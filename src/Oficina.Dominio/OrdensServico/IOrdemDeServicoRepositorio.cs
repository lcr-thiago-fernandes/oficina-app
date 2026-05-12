namespace Oficina.Dominio.OrdensServico;

public interface IOrdemDeServicoRepositorio
{
    Task<OrdemDeServico?> ObterPorIdAsync(Guid id, CancellationToken ct);
    Task<OrdemDeServico?> ObterPorNumeroAsync(long numero, CancellationToken ct);
    Task<IReadOnlyList<OrdemDeServico>> ListarAsync(StatusOrdemDeServico? statusFiltro, int pagina, int tamanhoPagina, CancellationToken ct);
    Task<int> ContarAsync(StatusOrdemDeServico? statusFiltro, CancellationToken ct);
    Task AdicionarAsync(OrdemDeServico ordem, CancellationToken ct);
    Task SalvarAsync(CancellationToken ct);

    Task EmTransacaoSerializadaAsync(Func<CancellationToken, Task> acao, CancellationToken ct);

    Task<MetricaTempoMedio> ObterTempoMedioExecucaoAsync(CancellationToken ct);

    // Forca o estado Added para itens novos adicionados via navigation
    // collection (workaround para bug de change detection com Id pre-setado).
    void MarcarItemServicoComoNovo(ItemServico item);
    void MarcarItemPecaComoNovo(ItemPeca item);
}

public sealed record MetricaTempoMedio(
    int TotalOrdensConcluidas,
    TimeSpan? TempoMedio,
    TimeSpan? TempoMinimo,
    TimeSpan? TempoMaximo);
