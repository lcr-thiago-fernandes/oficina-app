using Oficina.Aplicacao.OrdensServico.Dtos;
using Oficina.Dominio.Estoque;
using Oficina.Dominio.OrdensServico;

namespace Oficina.Aplicacao.OrdensServico;

public class AdicionarItemPecaUseCase
{
    private readonly IOrdemDeServicoRepositorio _ordens;
    private readonly IPecaRepositorio _pecas;

    public AdicionarItemPecaUseCase(IOrdemDeServicoRepositorio ordens, IPecaRepositorio pecas)
    {
        _ordens = ordens;
        _pecas = pecas;
    }

    public async Task<ItemPecaResponse?> ExecutarAsync(Guid ordemId, AdicionarItemPecaRequest req, CancellationToken ct)
    {
        var ordem = await _ordens.ObterPorIdAsync(ordemId, ct);
        if (ordem is null) return null;

        var peca = await _pecas.ObterPorIdAsync(req.PecaId, ct)
            ?? throw new OrdemInvalidaException("Peça não encontrada.");
        if (!peca.Ativo)
            throw new OrdemInvalidaException("Peça inativa.");

        var item = ordem.AdicionarItemPeca(peca.Id, peca.Nome, peca.PrecoUnitario, req.Quantidade);
        _ordens.MarcarItemPecaComoNovo(item);
        await _ordens.SalvarAsync(ct);

        return MapeadorOrdem.MapearPeca(item);
    }
}
