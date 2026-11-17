using Microsoft.Extensions.Logging;
using Oficina.Aplicacao.OrdensServico.Saga.Telemetria;

namespace Oficina.Infraestrutura.Telemetria;

/// <summary>
/// Publica o passo da saga como custom event <c>SagaOrdemServico</c> do New Relic. Telemetria nunca derruba
/// o consumo: falha do agente vira log de aviso.
/// </summary>
public class PublicadorEventoSagaNewRelic : IPublicadorEventoSaga
{
    public const string NomeDoEvento = "SagaOrdemServico";

    private readonly ILogger<PublicadorEventoSagaNewRelic> _log;

    public PublicadorEventoSagaNewRelic(ILogger<PublicadorEventoSagaNewRelic> log) => _log = log;

    public void Publicar(EventoSagaOrdemServico evento)
    {
        try
        {
            NewRelic.Api.Agent.NewRelic.RecordCustomEvent(NomeDoEvento, MontarAtributos(evento));
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex,
                "Falha ao publicar {Evento} da OS {OrdemServicoId} (passo {Passo}) — seguindo sem telemetria.",
                NomeDoEvento, evento.OrdemServicoId, evento.Passo);
        }
    }

    public static Dictionary<string, object> MontarAtributos(EventoSagaOrdemServico evento)
    {
        var atributos = new Dictionary<string, object>
        {
            ["ordemServicoId"] = evento.OrdemServicoId.ToString(),
            ["numeroOs"] = evento.NumeroOs,
            ["passo"] = evento.Passo,
            ["resultado"] = evento.Resultado,
            ["compensacao"] = evento.Compensacao,
            ["duracaoMs"] = evento.DuracaoMs,
            ["estado"] = evento.Estado,
        };

        if (evento.Motivo is not null)
            atributos["motivo"] = evento.Motivo;
        if (evento.Detalhe is not null)
            atributos["detalhe"] = evento.Detalhe;

        return atributos;
    }
}
