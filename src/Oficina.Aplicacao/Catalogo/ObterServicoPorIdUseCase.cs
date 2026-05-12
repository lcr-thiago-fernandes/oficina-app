using Oficina.Aplicacao.Catalogo.Dtos;
using Oficina.Dominio.Catalogo;

namespace Oficina.Aplicacao.Catalogo;

public class ObterServicoPorIdUseCase
{
    private readonly IServicoRepositorio _repo;
    public ObterServicoPorIdUseCase(IServicoRepositorio repo) => _repo = repo;

    public async Task<ServicoResponse?> ExecutarAsync(Guid id, CancellationToken ct)
    {
        var s = await _repo.ObterPorIdAsync(id, ct);
        return s is null ? null : MapeadorServicoResponse.Mapear(s);
    }
}
