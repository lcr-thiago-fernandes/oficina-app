using Oficina.Aplicacao.OrdensServico.Gateways;

namespace Oficina.Aplicacao.OrdensServico;

public class RemoverItemServicoUseCase
{
    private readonly IOrdemDeServicoGateway _ordens;
    public RemoverItemServicoUseCase(IOrdemDeServicoGateway ordens) => _ordens = ordens;

    public async Task<bool> ExecutarAsync(Guid ordemId, Guid itemId, CancellationToken ct)
    {
        var ordem = await _ordens.ObterPorIdAsync(ordemId, ct);
        if (ordem is null) return false;
        ordem.RemoverItemServico(itemId);
        await _ordens.SalvarAsync(ct);
        return true;
    }
}
