using Oficina.Aplicacao.Estoque.Dtos;
using Oficina.Dominio.Estoque;

namespace Oficina.Aplicacao.Estoque;

public class CriarPecaUseCase
{
    private readonly IPecaRepositorio _repo;
    public CriarPecaUseCase(IPecaRepositorio repo) => _repo = repo;

    public async Task<PecaResponse> ExecutarAsync(CriarPecaRequest req, CancellationToken ct)
    {
        var sku = Sku.Criar(req.Sku);

        if (await _repo.ExisteSkuAsync(sku, ct))
            throw new SkuJaCadastradoException(sku.Valor);

        var peca = Peca.Criar(sku, req.Nome, req.PrecoUnitario);
        await _repo.AdicionarAsync(peca, ct);
        await _repo.SalvarAsync(ct);

        return MapeadorEstoque.Mapear(peca);
    }
}
