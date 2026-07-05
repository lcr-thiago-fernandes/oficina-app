using Oficina.Aplicacao.Estoque;
using Oficina.Aplicacao.Estoque.Dtos;
using Oficina.Dominio.Estoque;

namespace Oficina.Adaptadores.Estoque.Presenters;

public static class PecaPresenter
{
    public static PecaResponse Apresentar(Peca p) =>
        new(p.Id, p.Sku.Valor, p.Nome, p.PrecoUnitario, p.SaldoAtual, p.Ativo, p.CriadoEm);

    public static MovimentacaoResponse ApresentarMovimentacao(MovimentacaoEstoque m) =>
        new(m.Id, m.Tipo.ToString(), m.Quantidade, m.Motivo, m.OrdemServicoId, m.CriadoEm);

    public static PaginaPecas ApresentarPagina(IReadOnlyList<Peca> itens, int total, int pagina, int tamanhoPagina) =>
        new(itens.Select(Apresentar).ToList(), total, pagina, tamanhoPagina);
}
