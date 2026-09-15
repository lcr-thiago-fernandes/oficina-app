using Oficina.Dominio.OrdensServico;

namespace Oficina.Aplicacao.OrdensServico.Gateways;

public interface IOrdemDeServicoGateway
{
    Task<OrdemDeServico?> ObterPorIdAsync(Guid id, CancellationToken ct);
    Task<OrdemDeServico?> ObterPorNumeroAsync(long numero, CancellationToken ct);
    Task<IReadOnlyList<OrdemDeServico>> ListarAsync(StatusOrdemDeServico? statusFiltro, int pagina, int tamanhoPagina, CancellationToken ct);

    /// <summary>Ordens de um cliente, da mais recente para a mais antiga, com itens carregados.</summary>
    Task<IReadOnlyList<OrdemDeServico>> ListarPorClienteAsync(Guid clienteId, CancellationToken ct);

    Task<int> ContarAsync(StatusOrdemDeServico? statusFiltro, CancellationToken ct);
    Task AdicionarAsync(OrdemDeServico ordem, CancellationToken ct);
    Task SalvarAsync(CancellationToken ct);

    /// <summary>
    /// Executa uma operação em transação serializável (baixa de estoque + mudança
    /// de estado da OS na mesma transação).
    /// </summary>
    Task EmTransacaoSerializadaAsync(Func<CancellationToken, Task> acao, CancellationToken ct);

    Task<MetricaTempoMedio> ObterTempoMedioExecucaoAsync(CancellationToken ct);

    // Forca o estado Added para itens novos adicionados via navigation
    // collection (workaround para bug de change detection com Id pre-setado).
    void MarcarItemServicoComoNovo(ItemServico item);
    void MarcarItemPecaComoNovo(ItemPeca item);
}

// Novo lar do record (antes vivia em Oficina.Dominio.OrdensServico/IOrdemDeServicoRepositorio.cs).
public sealed record MetricaTempoMedio(
    int TotalOrdensConcluidas,
    TimeSpan? TempoMedio,
    TimeSpan? TempoMinimo,
    TimeSpan? TempoMaximo);
