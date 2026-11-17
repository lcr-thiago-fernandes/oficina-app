using Amazon.Runtime;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Amazon.SQS;
using Amazon.SQS.Model;
using FluentAssertions;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Oficina.Infraestrutura.Mensageria;
using Oficina.Infraestrutura.Mensageria.Contratos;
using Oficina.Infraestrutura.Mensageria.Internos;
using Oficina.Infraestrutura.Persistencia;
using Testcontainers.LocalStack;

namespace Oficina.Infraestrutura.Testes.Mensageria;

/// <summary>
/// Sobe o bus real (Transporte=AmazonSqs, composição de produção via <c>AdicionarMensageria</c>) contra o
/// LocalStack e confere os nomes finais no SNS/SQS. O banco é inexistente: todo consumo da saga falha
/// (é o que o teste da fila <c>_error</c> precisa).
/// </summary>
public sealed class LocalStackFixture : IAsyncLifetime
{
    public const string Prefixo = "test";

    private readonly LocalStackContainer _container = new LocalStackBuilder()
        .WithImage("localstack/localstack:3.8.1")
        .WithEnvironment("SERVICES", "sqs,sns")
        .Build();

    private ServiceProvider? _provider;

    public IBusControl Bus { get; private set; } = null!;
    public IAmazonSimpleNotificationService Sns { get; private set; } = null!;
    public IAmazonSQS Sqs { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        var serviceUrl = _container.GetConnectionString();

        var configuracao = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Mensageria:Transporte"] = "AmazonSqs",
                ["Mensageria:PrefixoAmbiente"] = Prefixo,
                ["Mensageria:ServiceUrl"] = serviceUrl,
                // Retry e redelivery encurtados (1 s / 4 s / 10 s) para a falha chegar à _error no teste.
                ["Mensageria:IntervaloMinimoRetry"] = "00:00:00.010",
                ["Mensageria:IntervaloBaseRedelivery"] = "00:00:01",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<OficinaDbContext>(o =>
            o.UseNpgsql("Host=127.0.0.1;Port=1;Database=x;Username=x;Password=x;Timeout=2"));
        services.AdicionarMensageria(configuracao);
        _provider = services.BuildServiceProvider(true);

        Bus = _provider.GetRequiredService<IBusControl>();
        await Bus.StartAsync(new CancellationTokenSource(TimeSpan.FromSeconds(60)).Token);

        var credenciais = new BasicAWSCredentials("test", "test");
        Sns = new AmazonSimpleNotificationServiceClient(credenciais,
            new AmazonSimpleNotificationServiceConfig { ServiceURL = serviceUrl, AuthenticationRegion = "us-east-1" });
        Sqs = new AmazonSQSClient(credenciais,
            new AmazonSQSConfig { ServiceURL = serviceUrl, AuthenticationRegion = "us-east-1" });
    }

    public async Task DisposeAsync()
    {
        if (Bus is not null)
            await Bus.StopAsync();
        if (_provider is not null)
            await _provider.DisposeAsync();
        Sns?.Dispose();
        Sqs?.Dispose();
        await _container.DisposeAsync();
    }

    public async Task<List<string>> ListarTopicosAsync()
    {
        var arns = new List<string>();
        string? proximo = null;
        do
        {
            var r = await Sns.ListTopicsAsync(new ListTopicsRequest { NextToken = proximo });
            arns.AddRange((r.Topics ?? new List<Topic>()).Select(t => t.TopicArn));
            proximo = r.NextToken;
        } while (!string.IsNullOrEmpty(proximo));
        return arns;
    }

    public async Task<List<string>> ListarFilasAsync() =>
        (await Sqs.ListQueuesAsync(new ListQueuesRequest())).QueueUrls ?? new List<string>();

    public async Task<string> AguardarFilaAsync(string nome, TimeSpan limite)
    {
        var fim = DateTime.UtcNow + limite;
        while (DateTime.UtcNow < fim)
        {
            var url = (await ListarFilasAsync()).FirstOrDefault(u => u.EndsWith("/" + nome));
            if (url is not null)
                return url;
            await Task.Delay(500);
        }

        throw new TimeoutException($"A fila {nome} não foi criada em {limite}.");
    }
}

public sealed class TopologiaLocalStackTestes : IClassFixture<LocalStackFixture>
{
    /// <summary>Eventos que a saga assina (os 12 eventos dos contratos; os 4 comandos ela só envia).</summary>
    private static readonly Type[] EventosAssinados =
    {
        typeof(OrcamentoGerado), typeof(OrcamentoFalhou), typeof(OrcamentoAprovado), typeof(OrcamentoRejeitado),
        typeof(PagamentoConfirmado), typeof(PagamentoRecusado), typeof(PagamentoEstornado), typeof(EstornoFalhou),
        typeof(ExecucaoEnfileirada), typeof(ExecucaoRecusada), typeof(StatusExecucaoAlterado), typeof(ExecucaoFinalizada),
    };

    private readonly LocalStackFixture _fx;

    public TopologiaLocalStackTestes(LocalStackFixture fx) => _fx = fx;

    private static string NomeFinal(string arnOuUrl, char separador) =>
        arnOuUrl[(arnOuUrl.LastIndexOf(separador) + 1)..];

    [Fact]
    public async Task AmazonSqs_SagaAssinaTopicosComPrefixoPorTipo()
    {
        var topicos = (await _fx.ListarTopicosAsync()).Select(t => NomeFinal(t, ':')).ToList();

        topicos.Should().Contain(EventosAssinados.Select(t => $"test-oficina-saga-{t.Name}"));
        topicos.Should().Contain(new[] { "test-oficina-os-OrdemServicoAberta", "test-oficina-os-CancelamentoSolicitado" });
        topicos.Should().OnlyContain(t => t.StartsWith("test-oficina-"));
        topicos.Should().NotContain(t => t.Contains("Fault"));
    }

    [Fact]
    public async Task AmazonSqs_FilaDaSagaEFilaDoBusPorInstancia()
    {
        var filaBus = new NomesMensageria(LocalStackFixture.Prefixo).FilaBus(NomesMensageria.InstanciaAtual());
        filaBus.Should().StartWith("test-oficina-os-bus-");
        _fx.Bus.Address.AbsolutePath.Should().Be("/" + filaBus);

        // A fila do bus é criada sob demanda; conectar um handler força a criação para conferir o nome real.
        var handle = _fx.Bus.ConnectHandler<ExecucaoFinalizada>(_ => Task.CompletedTask);
        try
        {
            await _fx.AguardarFilaAsync(filaBus, TimeSpan.FromSeconds(30));
        }
        finally
        {
            handle.Disconnect();
        }

        var nomes = (await _fx.ListarFilasAsync()).Select(u => NomeFinal(u, '/')).ToList();

        nomes.Should().Contain("test-oficina-os-saga");
        nomes.Should().Contain(filaBus);
        nomes.Should().OnlyContain(n => n.StartsWith("test-oficina-"));
    }

    [Fact]
    public async Task AmazonSqs_ConsumoFalhando_PassaPeloRedeliveryETerminaNaErrorSemTopicoFault()
    {
        await _fx.Bus.Publish(new StatusExecucaoAlterado
        {
            OrdemServicoId = Guid.NewGuid(),
            OcorridoEm = DateTime.UtcNow,
            StatusExecucao = StatusExecucao.EmDiagnostico,
        });

        // Redelivery 1 s + 4 s + 10 s (escala do teste) antes de ir para a _error.
        var urlErro = await _fx.AguardarFilaAsync("test-oficina-os-saga_error", TimeSpan.FromSeconds(120));
        var recebida = await _fx.Sqs.ReceiveMessageAsync(new ReceiveMessageRequest
        {
            QueueUrl = urlErro,
            WaitTimeSeconds = 10,
            MaxNumberOfMessages = 1,
        });
        recebida.Messages.Should().ContainSingle("a falha definitiva deve terminar na fila _error");
        var envelope = System.Text.Json.Nodes.JsonNode.Parse(recebida.Messages[0].Body)!;
        envelope["headers"]!["MT-Redelivery-Count"]!.ToString()
            .Should().Be("3", "passou pelos 3 redeliveries atrasados antes da _error");

        var topicos = (await _fx.ListarTopicosAsync()).Select(t => NomeFinal(t, ':')).ToList();
        topicos.Should().NotContain(t => t.Contains("Fault"));
        topicos.Should().OnlyContain(t => t.StartsWith("test-oficina-"));
        var filas = (await _fx.ListarFilasAsync()).Select(u => NomeFinal(u, '/')).ToList();
        filas.Should().OnlyContain(n => n.StartsWith("test-oficina-"));
        filas.Where(n => n.Contains("error")).Should().OnlyContain(n => n.EndsWith("_error"));
    }
}
