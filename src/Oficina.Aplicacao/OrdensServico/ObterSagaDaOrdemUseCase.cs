using Oficina.Aplicacao.OrdensServico.Saga;
using Oficina.Aplicacao.OrdensServico.Saga.Dtos;

namespace Oficina.Aplicacao.OrdensServico;

public class ObterSagaDaOrdemUseCase
{
    private readonly ISagaOrdemServicoGateway _saga;
    public ObterSagaDaOrdemUseCase(ISagaOrdemServicoGateway saga) => _saga = saga;

    public Task<EstadoSagaResponse?> ExecutarAsync(Guid id, CancellationToken ct) =>
        _saga.ObterEstadoAsync(id, ct);
}
