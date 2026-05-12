using Oficina.Aplicacao.OrdensServico.Dtos;
using Oficina.Dominio.OrdensServico;

namespace Oficina.Aplicacao.OrdensServico;

public class ObterOrdemPorIdUseCase
{
    private readonly IOrdemDeServicoRepositorio _repo;
    public ObterOrdemPorIdUseCase(IOrdemDeServicoRepositorio repo) => _repo = repo;

    public async Task<OrdemResponse?> ExecutarAsync(Guid id, CancellationToken ct)
    {
        var o = await _repo.ObterPorIdAsync(id, ct);
        return o is null ? null : MapeadorOrdem.Mapear(o);
    }
}
