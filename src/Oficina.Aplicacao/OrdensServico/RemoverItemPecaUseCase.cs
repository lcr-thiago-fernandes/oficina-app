using Oficina.Dominio.OrdensServico;

namespace Oficina.Aplicacao.OrdensServico;

public class RemoverItemPecaUseCase
{
    private readonly IOrdemDeServicoRepositorio _ordens;
    public RemoverItemPecaUseCase(IOrdemDeServicoRepositorio ordens) => _ordens = ordens;

    public async Task<bool> ExecutarAsync(Guid ordemId, Guid itemId, CancellationToken ct)
    {
        var ordem = await _ordens.ObterPorIdAsync(ordemId, ct);
        if (ordem is null) return false;
        ordem.RemoverItemPeca(itemId);
        await _ordens.SalvarAsync(ct);
        return true;
    }
}
