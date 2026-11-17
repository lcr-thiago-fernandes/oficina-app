using MassTransit;
using Microsoft.Extensions.Logging;
using Oficina.Infraestrutura.Mensageria.Contratos;

namespace Oficina.Infraestrutura.Mensageria.Observabilidade;

/// <summary>
/// Só logging e New Relic: abre um escopo de log com <c>ordemServicoId</c> e <c>correlationId</c> (header
/// <c>X-Correlation-Id</c> ou, na falta dele, o ConversationId) e marca a transação do agente com a OS.
/// A propagação do header para as mensagens enviadas é feita no <c>Publish</c>/<c>Send</c>, não aqui.
/// </summary>
public sealed class FiltroCorrelacaoConsumo<T> : IFilter<ConsumeContext<T>> where T : class
{
    public const string HeaderCorrelacao = "X-Correlation-Id";

    private readonly ILogger<FiltroCorrelacaoConsumo<T>> _log;

    public FiltroCorrelacaoConsumo(ILogger<FiltroCorrelacaoConsumo<T>> log) => _log = log;

    public async Task Send(ConsumeContext<T> context, IPipe<ConsumeContext<T>> next)
    {
        if (context.Message is not IMensagemDaSaga mensagem)
        {
            await next.Send(context);
            return;
        }

        var id = mensagem.OrdemServicoId;
        var correlationId = context.Headers.TryGetHeader(HeaderCorrelacao, out var valor) && valor is not null
            ? valor.ToString()!
            : context.ConversationId?.ToString() ?? string.Empty;

        MarcarTransacao(id);

        using (_log.BeginScope(new Dictionary<string, object>
               {
                   ["ordemServicoId"] = id,
                   ["correlationId"] = correlationId,
               }))
        {
            await next.Send(context);
        }
    }

    private void MarcarTransacao(Guid id)
    {
        try
        {
            NewRelic.Api.Agent.NewRelic.GetAgent().CurrentTransaction.AddCustomAttribute("ordemServicoId", id.ToString());
        }
        catch (Exception ex)
        {
            _log.LogDebug(ex, "Agente do New Relic indisponível ao marcar a transação da OS {OrdemServicoId}.", id);
        }
    }

    public void Probe(ProbeContext context) => context.CreateFilterScope("correlacaoConsumo");
}
