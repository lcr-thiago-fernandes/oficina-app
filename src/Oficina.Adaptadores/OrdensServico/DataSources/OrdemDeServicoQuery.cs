using Oficina.Dominio.OrdensServico;

namespace Oficina.Adaptadores.OrdensServico.DataSources;

// Regras de listagem de OS extraídas para expressões puras sobre IQueryable,
// traduzíveis pelo EF Core (CASE/WHERE) e testáveis em memória (LINQ-to-Objects).
public static class OrdemDeServicoQuery
{
    public static IQueryable<OrdemDeServico> AplicarFiltro(
        IQueryable<OrdemDeServico> query, StatusOrdemDeServico? statusFiltro)
    {
        if (statusFiltro.HasValue)
            return query.Where(o => o.Status == statusFiltro.Value);

        // Sem filtro: esconde as OS terminais (trabalho concluído/cancelado).
        return query.Where(o =>
            o.Status != StatusOrdemDeServico.Finalizada &&
            o.Status != StatusOrdemDeServico.Entregue &&
            o.Status != StatusOrdemDeServico.Cancelada);
    }

    // Prioridade: fila de trabalho do pátio — o que está em reparo primeiro, depois
    // diagnóstico, fila de execução, aguardando pagamento, aguardando aprovação e
    // recém-recebidas; empate por data de criação ascendente (mais antigas primeiro).
    public static IQueryable<OrdemDeServico> AplicarOrdenacao(IQueryable<OrdemDeServico> query) =>
        query
            .OrderBy(o =>
                o.Status == StatusOrdemDeServico.EmReparo ? 1 :
                o.Status == StatusOrdemDeServico.EmDiagnostico ? 2 :
                o.Status == StatusOrdemDeServico.EmFilaExecucao ? 3 :
                o.Status == StatusOrdemDeServico.AguardandoPagamento ? 4 :
                o.Status == StatusOrdemDeServico.AguardandoAprovacao ? 5 :
                o.Status == StatusOrdemDeServico.Recebida ? 6 : 7)
            .ThenBy(o => o.CriadaEm);
}
