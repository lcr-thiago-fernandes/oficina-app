using Oficina.Aplicacao.OrdensServico.Gateways;
using Oficina.Dominio.OrdensServico;

namespace Oficina.Aplicacao.OrdensServico;

public class ObterOrdemPorIdUseCase
{
    private readonly IOrdemDeServicoGateway _gateway;
    public ObterOrdemPorIdUseCase(IOrdemDeServicoGateway gateway) => _gateway = gateway;

    public Task<OrdemDeServico?> ExecutarAsync(Guid id, CancellationToken ct) =>
        _gateway.ObterPorIdAsync(id, ct);
}
