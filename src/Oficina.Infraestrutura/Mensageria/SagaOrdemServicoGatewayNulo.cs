using Oficina.Aplicacao.OrdensServico.Saga;
using Oficina.Aplicacao.OrdensServico.Saga.Dtos;

namespace Oficina.Infraestrutura.Mensageria;

/// <summary>
/// Temporário (apagado na Task 6, que registra o gateway MassTransit com outbox): mantém a Api
/// resolvendo <c>AbrirOrdemDeServicoUseCase</c> sem bus. Não enfileira nada e não há saga.
/// </summary>
public sealed class SagaOrdemServicoGatewayNulo : ISagaOrdemServicoGateway
{
    public Task IniciarAsync(Guid ordemServicoId, CancellationToken ct) => Task.CompletedTask;

    public Task SolicitarCancelamentoAsync(Guid ordemServicoId, string motivo, CancellationToken ct) => Task.CompletedTask;

    public Task<EstadoSagaResponse?> ObterEstadoAsync(Guid ordemServicoId, CancellationToken ct) =>
        Task.FromResult<EstadoSagaResponse?>(null);
}
