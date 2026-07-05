using Oficina.Aplicacao.Estoque.Dtos;
using Oficina.Aplicacao.Estoque.Gateways;
using Oficina.Dominio.Estoque;

namespace Oficina.Aplicacao.Estoque;

public class CriarPecaUseCase
{
    private readonly IPecaGateway _gateway;
    public CriarPecaUseCase(IPecaGateway gateway) => _gateway = gateway;

    public async Task<Peca> ExecutarAsync(CriarPecaRequest req, CancellationToken ct)
    {
        var sku = Sku.Criar(req.Sku);

        if (await _gateway.ExisteSkuAsync(sku, ct))
            throw new SkuJaCadastradoException(sku.Valor);

        var peca = Peca.Criar(sku, req.Nome, req.PrecoUnitario);
        await _gateway.AdicionarAsync(peca, ct);
        await _gateway.SalvarAsync(ct);

        return peca;
    }
}
