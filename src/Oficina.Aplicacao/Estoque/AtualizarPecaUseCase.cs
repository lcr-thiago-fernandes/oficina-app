using Oficina.Aplicacao.Estoque.Dtos;
using Oficina.Dominio.Estoque;

namespace Oficina.Aplicacao.Estoque;

public class AtualizarPecaUseCase
{
    private readonly IPecaRepositorio _repo;
    public AtualizarPecaUseCase(IPecaRepositorio repo) => _repo = repo;

    public async Task<PecaResponse?> ExecutarAsync(Guid id, AtualizarPecaRequest req, CancellationToken ct)
    {
        var p = await _repo.ObterPorIdAsync(id, ct);
        if (p is null) return null;

        p.AtualizarDados(req.Nome, req.PrecoUnitario);
        await _repo.SalvarAsync(ct);
        return MapeadorEstoque.Mapear(p);
    }
}
