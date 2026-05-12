using Oficina.Aplicacao.Estoque.Dtos;
using Oficina.Dominio.Estoque;

namespace Oficina.Aplicacao.Estoque;

internal static class MapeadorEstoque
{
    public static PecaResponse Mapear(Peca p) =>
        new(p.Id, p.Sku.Valor, p.Nome, p.PrecoUnitario, p.SaldoAtual, p.Ativo, p.CriadoEm);

    public static MovimentacaoResponse MapearMov(MovimentacaoEstoque m) =>
        new(m.Id, m.Tipo.ToString(), m.Quantidade, m.Motivo, m.OrdemServicoId, m.CriadoEm);
}
