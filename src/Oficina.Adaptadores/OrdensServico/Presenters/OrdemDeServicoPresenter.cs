using Oficina.Aplicacao.OrdensServico;
using Oficina.Aplicacao.OrdensServico.Dtos;
using Oficina.Aplicacao.OrdensServico.Gateways;
using Oficina.Dominio.OrdensServico;

namespace Oficina.Adaptadores.OrdensServico.Presenters;

public static class OrdemDeServicoPresenter
{
    public static OrdemResponse Apresentar(OrdemDeServico o) => new(
        o.Id, o.Numero, o.ClienteId, o.VeiculoId, o.Status.ToString(), o.Observacoes,
        o.TotalServicos, o.TotalPecas, o.TotalGeral,
        o.CriadaEm, o.DiagnosticadaEm, o.EnviadaAprovacaoEm,
        o.OrcamentoAprovadoEm, o.OrcamentoRejeitadoEm,
        o.IniciadaEm, o.FinalizadaEm, o.EntregueEm,
        o.ItensServico.Select(ApresentarItemServico).ToList(),
        o.ItensPeca.Select(ApresentarItemPeca).ToList());

    public static ItemServicoResponse ApresentarItemServico(ItemServico i) =>
        new(i.Id, i.ServicoId, i.ServicoNome, i.PrecoSnapshot, i.Quantidade, i.Subtotal);

    public static ItemPecaResponse ApresentarItemPeca(ItemPeca i) =>
        new(i.Id, i.PecaId, i.PecaNome, i.PrecoSnapshot, i.Quantidade, i.Subtotal);

    public static PaginaOrdens ApresentarPagina(IReadOnlyList<OrdemDeServico> itens, int total, int pagina, int tamanhoPagina) =>
        new(itens.Select(Apresentar).ToList(), total, pagina, tamanhoPagina);

    // Converte TimeSpan -> double minutos (lógica antes em ObterTempoMedioExecucaoUseCase).
    public static MetricasTempoMedioResponse ApresentarMetrica(MetricaTempoMedio m) => new(
        m.TotalOrdensConcluidas,
        m.TempoMedio?.TotalMinutes,
        m.TempoMinimo?.TotalMinutes,
        m.TempoMaximo?.TotalMinutes);
}
