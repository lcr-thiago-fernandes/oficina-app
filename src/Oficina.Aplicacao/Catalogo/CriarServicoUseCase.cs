using Oficina.Aplicacao.Catalogo.Dtos;
using Oficina.Dominio.Catalogo;

namespace Oficina.Aplicacao.Catalogo;

public class CriarServicoUseCase
{
    private readonly IServicoRepositorio _repo;
    public CriarServicoUseCase(IServicoRepositorio repo) => _repo = repo;

    public async Task<ServicoResponse> ExecutarAsync(CriarServicoRequest req, CancellationToken ct)
    {
        var s = Servico.Criar(req.Nome, req.Descricao, req.PrecoBase, req.TempoEstimadoMinutos);
        await _repo.AdicionarAsync(s, ct);
        await _repo.SalvarAsync(ct);
        return MapeadorServicoResponse.Mapear(s);
    }
}
