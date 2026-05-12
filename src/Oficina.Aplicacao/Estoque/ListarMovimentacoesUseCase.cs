using Oficina.Aplicacao.Estoque.Dtos;
using Oficina.Dominio.Estoque;

namespace Oficina.Aplicacao.Estoque;

public class ListarMovimentacoesUseCase
{
    private readonly IPecaRepositorio _repo;
    public ListarMovimentacoesUseCase(IPecaRepositorio repo) => _repo = repo;

    public async Task<IReadOnlyList<MovimentacaoResponse>?> ExecutarAsync(Guid pecaId, CancellationToken ct)
    {
        var p = await _repo.ObterPorIdAsync(pecaId, ct);
        if (p is null) return null;
        return p.Movimentacoes
            .OrderByDescending(m => m.CriadoEm)
            .Select(MapeadorEstoque.MapearMov)
            .ToList();
    }
}
