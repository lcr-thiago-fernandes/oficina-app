using System.Collections.Concurrent;
using FluentAssertions;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Oficina.Aplicacao.OrdensServico.Saga;
using Oficina.Aplicacao.OrdensServico.Saga.Dtos;
using Oficina.Infraestrutura.Mensageria;
using Oficina.Infraestrutura.Mensageria.Contratos;
using Oficina.Infraestrutura.Mensageria.Internos;
using Oficina.Infraestrutura.Mensageria.Observabilidade;
using Oficina.Infraestrutura.Mensageria.Saga;

namespace Oficina.Infraestrutura.Testes.Mensageria;

public sealed class CorrelacaoMensageriaTestes : IAsyncLifetime
{
    private readonly Guid _id = Guid.NewGuid();
    private readonly ProvedorDeLogsComEscopo _logs = new();
    private ServiceProvider _provider = null!;
    private ITestHarness _harness = null!;

    public async Task InitializeAsync()
    {
        var passos = new Mock<IPassosSagaOrdemServico>();
        passos.Setup(p => p.MontarPedidoOrcamentoAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PedidoOrcamento(1, "12345678909", "Maria", "m@x.com", new List<ItemPedidoOrcamento>()));

        _provider = new ServiceCollection()
            .AddLogging(l => l.AddProvider(_logs))
            .AddSingleton(Options.Create(new OpcoesMensageria { PrefixoAmbiente = "teste" }))
            .AddScoped(_ => passos.Object)
            .AddSingleton<IRegistradorPassoSaga>(new RegistradorPassoSagaFake())
            .AddMassTransitTestHarness(x =>
            {
                x.SetTestTimeouts(testTimeout: TimeSpan.FromSeconds(10), testInactivityTimeout: TimeSpan.FromMilliseconds(50));
                x.AddSagaStateMachine<OrdemServicoSaga, EstadoSagaOrdemServico>().InMemoryRepository();
                x.AddConfigureEndpointsCallback((ctx, _, cfg) => cfg.UseConsumeFilter(typeof(FiltroCorrelacaoConsumo<>), ctx));
            })
            .BuildServiceProvider(true);
        _harness = _provider.GetRequiredService<ITestHarness>();
        await _harness.Start();
    }

    public async Task DisposeAsync()
    {
        await _harness.Stop();
        await _provider.DisposeAsync();
    }

    [Fact]
    public async Task CorrelationIdDaMensagemDeEntrada_EPropagadoAoComandoEnviadoPelaSaga()
    {
        await _harness.Bus.Publish(new OrdemServicoAberta { OrdemServicoId = _id, OcorridoEm = DateTime.UtcNow },
            c => c.Headers.Set("X-Correlation-Id", "abc"));

        await AguardarAsync(async () => await _harness.Sent.Any<GerarOrcamento>());
        var enviado = _harness.Sent.Select<GerarOrcamento>().Single();
        enviado.Context.Headers.Get<string>("X-Correlation-Id").Should().Be("abc");
    }

    [Fact]
    public async Task SemCorrelationIdNaEntrada_ComandoEnviadoNaoCarregaOHeader()
    {
        await _harness.Bus.Publish(new OrdemServicoAberta { OrdemServicoId = _id, OcorridoEm = DateTime.UtcNow });

        await AguardarAsync(async () => await _harness.Sent.Any<GerarOrcamento>());
        _harness.Sent.Select<GerarOrcamento>().Single().Context.Headers.Get<string>("X-Correlation-Id").Should().BeNull();
    }

    [Fact]
    public async Task FiltroDeConsumo_AbreEscopoDeLogComOrdemServicoIdECorrelationId()
    {
        await _harness.Bus.Publish(new OrdemServicoAberta { OrdemServicoId = _id, OcorridoEm = DateTime.UtcNow },
            c => c.Headers.Set("X-Correlation-Id", "corr-1"));

        await AguardarAsync(async () => await _harness.Sent.Any<GerarOrcamento>());
        _logs.Escopos.Should().Contain(e =>
            e.ContainsKey("ordemServicoId") && (Guid)e["ordemServicoId"] == _id && (string)e["correlationId"] == "corr-1");
    }

    [Fact]
    public async Task FiltroDeConsumo_SemHeader_UsaConversationIdComoCorrelationId()
    {
        var conversa = NewId.NextGuid();
        await _harness.Bus.Publish(new OrdemServicoAberta { OrdemServicoId = _id, OcorridoEm = DateTime.UtcNow },
            c => c.ConversationId = conversa);

        await AguardarAsync(async () => await _harness.Sent.Any<GerarOrcamento>());
        _logs.Escopos.Should().Contain(e =>
            e.ContainsKey("ordemServicoId") && e["correlationId"].ToString() == conversa.ToString());
    }

    private static async Task AguardarAsync(Func<Task<bool>> condicao)
    {
        var limite = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < limite)
        {
            if (await condicao())
                return;
            await Task.Delay(20);
        }
        throw new TimeoutException("Condição não atendida em 5s.");
    }

    private sealed class ProvedorDeLogsComEscopo : ILoggerProvider
    {
        private readonly ConcurrentQueue<Dictionary<string, object>> _vistos = new();

        public IReadOnlyList<Dictionary<string, object>> Escopos => _vistos.ToList();

        public ILogger CreateLogger(string categoria) => new Registrador(this);
        public void Dispose() { }

        private sealed class Registrador : ILogger
        {
            private readonly ProvedorDeLogsComEscopo _pai;
            public Registrador(ProvedorDeLogsComEscopo pai) => _pai = pai;

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull
            {
                if (state is IEnumerable<KeyValuePair<string, object>> pares)
                    _pai._vistos.Enqueue(pares.ToDictionary(p => p.Key, p => p.Value));
                return null;
            }

            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
        }
    }
}
