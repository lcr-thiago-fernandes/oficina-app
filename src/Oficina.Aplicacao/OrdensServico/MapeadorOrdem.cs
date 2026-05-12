using Oficina.Aplicacao.OrdensServico.Dtos;
using Oficina.Dominio.OrdensServico;

namespace Oficina.Aplicacao.OrdensServico;

internal static class MapeadorOrdem
{
    public static OrdemResponse Mapear(OrdemDeServico o) => new(
        o.Id, o.Numero, o.ClienteId, o.VeiculoId, o.Status.ToString(), o.Observacoes,
        o.TotalServicos, o.TotalPecas, o.TotalGeral,
        o.CriadaEm, o.DiagnosticadaEm, o.EnviadaAprovacaoEm,
        o.OrcamentoAprovadoEm, o.OrcamentoRejeitadoEm,
        o.IniciadaEm, o.FinalizadaEm, o.EntregueEm,
        o.ItensServico.Select(MapearServ).ToList(),
        o.ItensPeca.Select(MapearPeca).ToList());

    public static ItemServicoResponse MapearServ(ItemServico i) =>
        new(i.Id, i.ServicoId, i.ServicoNome, i.PrecoSnapshot, i.Quantidade, i.Subtotal);

    public static ItemPecaResponse MapearPeca(ItemPeca i) =>
        new(i.Id, i.PecaId, i.PecaNome, i.PrecoSnapshot, i.Quantidade, i.Subtotal);
}
