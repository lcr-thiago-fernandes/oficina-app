using Oficina.Dominio.Estoque;

namespace Oficina.Aplicacao.Estoque;

public class RemoverPecaUseCase
{
    private readonly IPecaRepositorio _repo;
    public RemoverPecaUseCase(IPecaRepositorio repo) => _repo = repo;

    public async Task<bool> ExecutarAsync(Guid id, CancellationToken ct)
    {
        var p = await _repo.ObterPorIdAsync(id, ct);
        if (p is null) return false;

        p.Inativar();
        await _repo.SalvarAsync(ct);
        return true;
    }
}
