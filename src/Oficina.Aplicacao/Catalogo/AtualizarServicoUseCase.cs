using Oficina.Aplicacao.Catalogo.Dtos;
using Oficina.Dominio.Catalogo;

namespace Oficina.Aplicacao.Catalogo;

public class AtualizarServicoUseCase
{
    private readonly IServicoRepositorio _repo;
    public AtualizarServicoUseCase(IServicoRepositorio repo) => _repo = repo;

    public async Task<ServicoResponse?> ExecutarAsync(Guid id, AtualizarServicoRequest req, CancellationToken ct)
    {
        var s = await _repo.ObterPorIdAsync(id, ct);
        if (s is null) return null;

        s.AtualizarDados(req.Nome, req.Descricao, req.PrecoBase, req.TempoEstimadoMinutos);
        await _repo.SalvarAsync(ct);
        return MapeadorServicoResponse.Mapear(s);
    }
}
