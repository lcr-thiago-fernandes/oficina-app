using System.Collections.Concurrent;
using MassTransit;
using Oficina.Infraestrutura.Mensageria;
using Oficina.Infraestrutura.Mensageria.Contratos;

namespace Oficina.Integracao.Testes.Saga;

/// <summary>
/// O que os testes de integração precisam ver do bus e o harness não mostra:
/// <list type="bullet">
/// <item>mensagens entregues pelo outbox chegam ao harness como <c>SerializedMessageBody</c> (Sent/Published
/// não as tipam): os comandos são capturados por endpoints que fazem o papel do Billing e da Execução;</item>
/// <item>com retry/redelivery o harness (e os observadores de consumo) só registram a falha no fim de todas
/// as tentativas: as tentativas são contadas por um filtro do endpoint, por dentro do retry.</item>
/// </list>
/// </summary>
public sealed class ObservadorDaSaga : IFilter<ConsumeContext>
{
    private readonly ConcurrentDictionary<Guid, int> _tentativasPorMensagem = new();
    private readonly ConcurrentDictionary<string, int> _tentativasPorTipo = new();
    private readonly ConcurrentQueue<ComandoRecebido> _comandos = new();

    public sealed record ComandoRecebido(object Mensagem, Guid OrdemServicoId, Uri? Destino, Uri? FaultAddress);

    public IReadOnlyList<ComandoRecebido> Comandos => _comandos.ToList();

    public IEnumerable<(ComandoRecebido Recebido, T Mensagem)> ComandosDoTipo<T>(Guid osId) where T : class, IMensagemDaSaga =>
        _comandos.Where(c => c.Mensagem is T && c.OrdemServicoId == osId).Select(c => (c, (T)c.Mensagem));

    /// <summary>Quantas vezes a mensagem passou pelo pipe de consumo (cada retry e redelivery conta).</summary>
    public int Tentativas(Guid messageId) => _tentativasPorMensagem.GetValueOrDefault(messageId);

    public int TotalDeTentativas<T>() => _tentativasPorTipo.GetValueOrDefault(MessageUrn.ForTypeString<T>());

    public void ConectarComandos(IBus bus, NomesMensageria nomes)
    {
        bus.ConnectReceiveEndpoint(nomes.FilaComandosBilling, e =>
        {
            e.Handler<GerarOrcamento>(Capturar);
            e.Handler<CancelarOrcamento>(Capturar);
            e.Handler<EstornarPagamento>(Capturar);
        });
        bus.ConnectReceiveEndpoint(nomes.FilaComandosExecucao, e => e.Handler<IniciarExecucao>(Capturar));
    }

    private Task Capturar<T>(ConsumeContext<T> ctx) where T : class, IMensagemDaSaga
    {
        _comandos.Enqueue(new ComandoRecebido(ctx.Message, ctx.Message.OrdemServicoId, ctx.DestinationAddress, ctx.FaultAddress));
        return Task.CompletedTask;
    }

    public async Task Send(ConsumeContext context, IPipe<ConsumeContext> next)
    {
        if (context.MessageId is { } id)
            _tentativasPorMensagem.AddOrUpdate(id, 1, (_, n) => n + 1);
        foreach (var tipo in context.SupportedMessageTypes)
            _tentativasPorTipo.AddOrUpdate(tipo, 1, (_, n) => n + 1);
        await next.Send(context);
    }

    public void Probe(ProbeContext context) => context.CreateFilterScope("observadorDaSaga");
}
