using Oficina.Aplicacao.OrdensServico.Saga.Dtos;

namespace Oficina.Aplicacao.OrdensServico.Saga;

/// <summary>
/// Porta de saída para a saga orquestrada da OS. <see cref="IniciarAsync"/> e
/// <see cref="SolicitarCancelamentoAsync"/> só <b>enfileiram</b> a mensagem no outbox:
/// quem a persiste (junto com a mudança de estado da OS) é o <c>SalvarAsync</c> seguinte do chamador.
/// </summary>
public interface ISagaOrdemServicoGateway
{
    Task IniciarAsync(Guid ordemServicoId, CancellationToken ct);
    Task SolicitarCancelamentoAsync(Guid ordemServicoId, string motivo, CancellationToken ct);

    /// <summary>Estado atual da saga, ou <c>null</c> se ela ainda não foi criada.</summary>
    Task<EstadoSagaResponse?> ObterEstadoAsync(Guid ordemServicoId, CancellationToken ct);
}
