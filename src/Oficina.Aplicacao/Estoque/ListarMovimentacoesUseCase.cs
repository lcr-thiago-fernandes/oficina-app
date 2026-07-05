using Oficina.Aplicacao.Estoque.Gateways;
using Oficina.Dominio.Estoque;

namespace Oficina.Aplicacao.Estoque;

public class ListarMovimentacoesUseCase
{
    private readonly IPecaGateway _gateway;
    public ListarMovimentacoesUseCase(IPecaGateway gateway) => _gateway = gateway;

    public async Task<IReadOnlyList<MovimentacaoEstoque>?> ExecutarAsync(Guid pecaId, CancellationToken ct)
    {
        var p = await _gateway.ObterPorIdAsync(pecaId, ct);
        if (p is null) return null;
        return p.Movimentacoes
            .OrderByDescending(m => m.CriadoEm)
            .ToList();
    }
}
