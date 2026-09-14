namespace Oficina.Aplicacao.OrdensServico.Telemetria;

/// <summary>
/// Porta de telemetria de negócio. A implementação é da infraestrutura —
/// a camada de aplicação não conhece o New Relic.
/// </summary>
public interface IPublicadorEventoOs
{
    void Publicar(EventoOrdemServico evento);
}
