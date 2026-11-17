using System.Reflection;
using FluentAssertions;
using MassTransit;
using MassTransit.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Oficina.Aplicacao.OrdensServico.Saga;
using Oficina.Dominio;
using Oficina.Infraestrutura.Mensageria;
using Oficina.Infraestrutura.Mensageria.Contratos;
using Oficina.Infraestrutura.Mensageria.Internos;
using Oficina.Infraestrutura.Mensageria.Saga;
using Oficina.Infraestrutura.Persistencia;
using Oficina.Testes.Compartilhado;

namespace Oficina.Infraestrutura.Testes.Mensageria;

/// <summary>
/// Composição do MassTransit (o bus é construído pelo ServiceProvider; não precisa ser iniciado para
/// ler a topologia). O DbContext aponta para um banco inexistente: nada aqui consome mensagens da saga.
/// </summary>
public class DependencyInjectionMensageriaTestes
{
    private const string ConexaoFicticia = "Host=127.0.0.1;Port=1;Database=x;Username=x;Password=x";

    public static TheoryData<Type> TiposConfigurados()
    {
        var dados = new TheoryData<Type>();
        foreach (var tipo in TopologiaSaga.TiposConfigurados)
            dados.Add(tipo);
        return dados;
    }

    private static IConfiguration Configuracao(string prefixo = "local") =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Mensageria:Transporte"] = "InMemory",
                ["Mensageria:PrefixoAmbiente"] = prefixo,
            })
            .Build();

    private static ServiceCollection Servicos(string prefixo = "local")
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<OficinaDbContext>(o => o.UseNpgsql(ConexaoFicticia));
        services.AddSingleton<Oficina.Aplicacao.OrdensServico.Saga.Telemetria.IPublicadorEventoSaga, Oficina.Infraestrutura.Telemetria.PublicadorEventoSagaNewRelic>();
        services.AdicionarMensageria(Configuracao(prefixo));
        return services;
    }

    private static string NomeDaEntidade(IBus bus, Type tipo)
    {
        var topologia = typeof(IBusTopology).GetMethod(nameof(IBusTopology.Message))!
            .MakeGenericMethod(tipo)
            .Invoke(bus.Topology, null)!;
        return (string)topologia.GetType().GetProperty("EntityName")!.GetValue(topologia)!;
    }

    [Fact]
    public async Task AdicionarMensageria_TransporteInMemory_RegistraBusESaga()
    {
        await using var provider = Servicos().BuildServiceProvider(true);

        provider.GetService<IBusControl>().Should().NotBeNull();
        provider.GetRequiredService<NomesMensageria>().FilaSaga.Should().Be("local-oficina-os-saga");
        using var scope = provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<ISagaOrdemServicoGateway>().Should().BeOfType<SagaOrdemServicoGateway>();
        scope.ServiceProvider.GetRequiredService<IRegistradorPassoSaga>().Should().BeOfType<RegistradorPassoSaga>();
    }

    [Theory]
    [InlineData("Prod!")]
    [InlineData("hml\n")]
    [InlineData("HML")]
    public void AdicionarMensageria_PrefixoInvalido_LancaNaInicializacao(string prefixo)
    {
        var acao = () => new ServiceCollection().AdicionarMensageria(Configuracao(prefixo));

        acao.Should().Throw<ArgumentException>().WithMessage("*PrefixoAmbiente*");
    }

    [Fact]
    public void AdicionarMensageria_TransporteDesconhecido_Lanca()
    {
        var configuracao = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Mensageria:Transporte"] = "Kafka" })
            .Build();

        var acao = () => new ServiceCollection().AdicionarMensageria(configuracao);

        acao.Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [MemberData(nameof(TiposConfigurados))]
    public async Task Topologia_ComPrefixoHml_NomeFinalDoTopicoDeCadaTipo(Type tipo)
    {
        await using var provider = Servicos("hml").BuildServiceProvider(true);
        var bus = provider.GetRequiredService<IBus>();

        var interno = tipo == typeof(OrdemServicoAberta) || tipo == typeof(CancelamentoSolicitado);
        NomeDaEntidade(bus, tipo).Should().Be(interno ? $"hml-oficina-os-{tipo.Name}" : $"hml-oficina-saga-{tipo.Name}");
    }

    [Fact]
    public void Topologia_CobreOs16ContratosEOs2Internos()
    {
        var contratos = ContratosMensagensTestes.TiposDeContrato.Select(d => (Type)d[0]);

        TopologiaSaga.TiposConfigurados.Should().BeEquivalentTo(
            contratos.Concat(new[] { typeof(OrdemServicoAberta), typeof(CancelamentoSolicitado) }));
        TopologiaSaga.TiposConfigurados.Should().HaveCount(18).And.OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task SemTopologiaSaga_AtributoSozinhoNaoTemPrefixo()
    {
        await using var provider = new ServiceCollection()
            .AddMassTransit(x => x.UsingInMemory((ctx, cfg) => cfg.ConfigureEndpoints(ctx)))
            .BuildServiceProvider(true);
        var bus = provider.GetRequiredService<IBus>();

        // Documenta por que o prefixo é aplicado por tipo: o [EntityName] sozinho não tem o ambiente.
        NomeDaEntidade(bus, typeof(OrcamentoGerado)).Should().Be("oficina-saga-OrcamentoGerado");
    }

    [Fact]
    public async Task HarnessComConfigurarRegistro_MantemRepositorioEfDaSaga()
    {
        var services = Servicos();
        var acao = () => services.SubstituirMensageriaPeloHarness(new OpcoesMensageria { IntervaloOutboxMs = 100 });

        acao.Should().NotThrow();
        await using var provider = services.BuildServiceProvider(true);
        using var scope = provider.CreateScope();
        // ISagaRepository<T> é um invólucro genérico; quem decide onde a saga mora é a fábrica de contexto.
        scope.ServiceProvider.GetRequiredService<ISagaRepository<EstadoSagaOrdemServico>>().Should().NotBeNull();
        var fabrica = scope.ServiceProvider.GetRequiredService<MassTransit.Saga.ISagaRepositoryContextFactory<EstadoSagaOrdemServico>>();

        fabrica.GetType().Namespace.Should().StartWith("MassTransit.EntityFrameworkCoreIntegration");
    }

    [Fact]
    public void SubstituirMensageriaPeloHarness_DeixaUmSoCallbackDeEndpointEUmaSoNotificacaoDoOutbox()
    {
        var services = Servicos();
        int Contar<T>() => services.Count(d => d.ServiceType == typeof(T));
        // Sem a limpeza do helper, o AddMassTransitTestHarness deixaria os dois (o de produção primeiro).
        Contar<MassTransit.DependencyInjection.Bind<IBus, IConfigureReceiveEndpoint>>().Should().Be(1);

        services.SubstituirMensageriaPeloHarness(new OpcoesMensageria());

        Contar<MassTransit.DependencyInjection.Bind<IBus, IConfigureReceiveEndpoint>>().Should().Be(1);
        Contar<MassTransit.Middleware.Outbox.IBusOutboxNotification>().Should().Be(1);
    }

    [Fact]
    public void Opcoes_Padrao_ReproduzemRetryERedeliveryDasGlobalConstraints()
    {
        var opcoes = new OpcoesMensageria();

        opcoes.Transporte.Should().Be(TransporteMensageria.InMemory);
        opcoes.PrefixoAmbiente.Should().Be("local");
        opcoes.Regiao.Should().Be("us-east-1");
        opcoes.ServiceUrl.Should().BeNull();
        opcoes.IntervaloOutboxMs.Should().Be(1000);
        DependencyInjectionMensageria.ParametrosRetry(opcoes).Should().Be(
            (3, TimeSpan.FromMilliseconds(200), TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(500)));
        DependencyInjectionMensageria.IntervalosRedelivery(opcoes).Should().Equal(
            TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(5));
    }

    [Fact]
    public async Task Retentativas_ConflitoDeConcorrencia_ERetentadoSemPublicarFault()
    {
        await using var h = await HarnessDeRetentativasAsync(new DbUpdateConcurrencyException("xmin mudou"));

        await h.Harness.Bus.Publish(new OrcamentoGerado { OrdemServicoId = Guid.NewGuid(), OcorridoEm = DateTime.UtcNow });

        await h.AguardarTentativasAsync(16);
        await Task.Delay(500);
        h.Consumidor.Tentativas.Should().Be(16, "(1 entrega + 3 retries) x (1 + 3 redeliveries)");
        h.Harness.Published.Select<Fault>().Should().BeEmpty("PublishFaults = false no endpoint");
    }

    [Fact]
    public async Task Retentativas_ExcecaoDeDominio_NaoERetentada()
    {
        await using var h = await HarnessDeRetentativasAsync(new FalhaDeDominioDeTeste());

        await h.Harness.Bus.Publish(new OrcamentoGerado { OrdemServicoId = Guid.NewGuid(), OcorridoEm = DateTime.UtcNow });

        await h.AguardarTentativasAsync(4);
        await Task.Delay(500);
        h.Consumidor.Tentativas.Should().Be(4, "sem retry imediato; só 1 entrega + 3 redeliveries");
        h.Harness.Published.Select<Fault>().Should().BeEmpty();
    }

    [Fact]
    public void AwsSdkSecurityToken_EstaPresenteNaMesmaFamiliaDoAwsSdkCore()
    {
        // Sem AWSSDK.SecurityToken a cadeia padrão de credenciais não resolve o web identity do IRSA no EKS.
        var core = typeof(Amazon.Runtime.AWSCredentials).Assembly.GetName();
        var sts = Assembly.Load(new AssemblyName("AWSSDK.SecurityToken")).GetName();

        sts.Version!.Major.Should().Be(core.Version!.Major);
        Type.GetType("Amazon.SecurityToken.AmazonSecurityTokenServiceClient, AWSSDK.SecurityToken", throwOnError: true)
            .Should().NotBeNull();
    }

    // ---------------------------------------------------------------- helpers

    private sealed class FalhaDeDominioDeTeste : ExcecaoDeDominio
    {
        public FalhaDeDominioDeTeste() : base("regra violada") { }
    }

    private sealed class ConsumidorQueLanca : IConsumer<OrcamentoGerado>
    {
        private readonly Exception _excecao;
        private int _tentativas;

        public ConsumidorQueLanca(Exception excecao) => _excecao = excecao;

        public int Tentativas => _tentativas;

        public Task Consume(ConsumeContext<OrcamentoGerado> context)
        {
            Interlocked.Increment(ref _tentativas);
            throw _excecao;
        }
    }

    private sealed class HarnessDeRetentativas : IAsyncDisposable
    {
        public required ServiceProvider Provider { get; init; }
        public required ITestHarness Harness { get; init; }
        public required ConsumidorQueLanca Consumidor { get; init; }

        public async Task AguardarTentativasAsync(int minimo)
        {
            var limite = DateTime.UtcNow.AddSeconds(10);
            while (Consumidor.Tentativas < minimo && DateTime.UtcNow < limite)
                await Task.Delay(10);
            Consumidor.Tentativas.Should().BeGreaterThanOrEqualTo(minimo);
        }

        public async ValueTask DisposeAsync()
        {
            await Harness.Stop();
            await Provider.DisposeAsync();
        }
    }

    /// <summary>
    /// Endpoint com as mesmas políticas de produção (sem o outbox, que exige banco), retry e redelivery em
    /// escala de teste: a falha esgota tudo, e só então o MassTransit publicaria o Fault.
    /// </summary>
    private static async Task<HarnessDeRetentativas> HarnessDeRetentativasAsync(Exception excecao)
    {
        var consumidor = new ConsumidorQueLanca(excecao);
        var opcoes = new OpcoesMensageria
        {
            IntervaloMinimoRetry = TimeSpan.FromMilliseconds(5),
            IntervaloBaseRedelivery = TimeSpan.FromMilliseconds(20),
        };
        var provider = new ServiceCollection()
            .AddMassTransitTestHarness(x =>
            {
                x.UsingInMemory((ctx, cfg) =>
                {
                    cfg.UseDelayedMessageScheduler();
                    cfg.ReceiveEndpoint("teste-retentativas", e =>
                    {
                        DependencyInjectionMensageria.ConfigurarRetentativas(e, opcoes);
                        e.Consumer(() => consumidor);
                    });
                });
            })
            .BuildServiceProvider(true);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        return new HarnessDeRetentativas { Provider = provider, Harness = harness, Consumidor = consumidor };
    }
}
