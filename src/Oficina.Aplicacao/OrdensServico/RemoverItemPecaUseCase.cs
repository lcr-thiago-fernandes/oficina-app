using Oficina.Aplicacao.OrdensServico.Gateways;

namespace Oficina.Aplicacao.OrdensServico;

public class RemoverItemPecaUseCase
{
    private readonly IOrdemDeServicoGateway _ordens;
    public RemoverItemPecaUseCase(IOrdemDeServicoGateway ordens) => _ordens = ordens;

    public async Task<bool> ExecutarAsync(Guid ordemId, Guid itemId, CancellationToken ct)
    {
        var ordem = await _ordens.ObterPorIdAsync(ordemId, ct);
        if (ordem is null) return false;
        ordem.RemoverItemPeca(itemId);
        await _ordens.SalvarAsync(ct);
        return true;
    }
}
