using Oficina.Dominio.Catalogo;

namespace Oficina.Aplicacao.Catalogo;

public class RemoverServicoUseCase
{
    private readonly IServicoRepositorio _repo;
    public RemoverServicoUseCase(IServicoRepositorio repo) => _repo = repo;

    public async Task<bool> ExecutarAsync(Guid id, CancellationToken ct)
    {
        var s = await _repo.ObterPorIdAsync(id, ct);
        if (s is null) return false;

        s.Inativar();
        await _repo.SalvarAsync(ct);
        return true;
    }
}
