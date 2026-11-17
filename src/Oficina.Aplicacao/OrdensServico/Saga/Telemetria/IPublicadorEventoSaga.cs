namespace Oficina.Aplicacao.OrdensServico.Saga.Telemetria;

/// <summary>Porta de telemetria da saga: a implementação é da infraestrutura (New Relic).</summary>
public interface IPublicadorEventoSaga
{
    void Publicar(EventoSagaOrdemServico evento);
}
