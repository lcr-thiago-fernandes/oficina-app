using Microsoft.Extensions.Logging;
using Oficina.Aplicacao.OrdensServico.Telemetria;

namespace Oficina.Infraestrutura.Telemetria;

/// <summary>
/// Publica o evento como custom event do New Relic. Telemetria nunca derruba
/// requisição: qualquer falha aqui vira log de aviso e segue o fluxo.
/// </summary>
public class PublicadorEventoOsNewRelic : IPublicadorEventoOs
{
    private const string NomeDoEvento = "OrdemServicoEvento";

    private readonly ILogger<PublicadorEventoOsNewRelic> _log;

    public PublicadorEventoOsNewRelic(ILogger<PublicadorEventoOsNewRelic> log) => _log = log;

    public void Publicar(EventoOrdemServico evento)
    {
        var atributos = new Dictionary<string, object>
        {
            ["numeroOs"] = evento.NumeroOs,
            ["statusAnterior"] = evento.StatusAnterior ?? "(inicial)",
            ["statusNovo"] = evento.StatusNovo,
            ["resultado"] = evento.Resultado,
            ["unidade"] = evento.Unidade
        };

        if (evento.DuracaoNoStatusSegundos.HasValue)
            atributos["duracaoNoStatusSegundos"] = evento.DuracaoNoStatusSegundos.Value;

        try
        {
            NewRelic.Api.Agent.NewRelic.RecordCustomEvent(NomeDoEvento, atributos);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex,
                "Falha ao publicar {Evento} da OS {NumeroOs} — seguindo sem telemetria.",
                NomeDoEvento, evento.NumeroOs);
        }
    }
}
