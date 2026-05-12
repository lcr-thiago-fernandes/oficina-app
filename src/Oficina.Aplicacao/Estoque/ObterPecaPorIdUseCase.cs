using Oficina.Aplicacao.Estoque.Dtos;
using Oficina.Dominio.Estoque;

namespace Oficina.Aplicacao.Estoque;

public class ObterPecaPorIdUseCase
{
    private readonly IPecaRepositorio _repo;
    public ObterPecaPorIdUseCase(IPecaRepositorio repo) => _repo = repo;

    public async Task<PecaResponse?> ExecutarAsync(Guid id, CancellationToken ct)
    {
        var p = await _repo.ObterPorIdAsync(id, ct);
        return p is null ? null : MapeadorEstoque.Mapear(p);
    }
}
