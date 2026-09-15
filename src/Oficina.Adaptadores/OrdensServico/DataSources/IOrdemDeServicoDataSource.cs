using Oficina.Aplicacao.OrdensServico.Gateways;
using Oficina.Dominio.OrdensServico;

namespace Oficina.Adaptadores.OrdensServico.DataSources;

public interface IOrdemDeServicoDataSource
{
    Task<OrdemDeServico?> ObterPorIdAsync(Guid id, CancellationToken ct);
    Task<OrdemDeServico?> ObterPorNumeroAsync(long numero, CancellationToken ct);
    Task<IReadOnlyList<OrdemDeServico>> ListarAsync(StatusOrdemDeServico? statusFiltro, int pagina, int tamanhoPagina, CancellationToken ct);
    Task<IReadOnlyList<OrdemDeServico>> ListarPorClienteAsync(Guid clienteId, CancellationToken ct);
    Task<int> ContarAsync(StatusOrdemDeServico? statusFiltro, CancellationToken ct);
    Task AdicionarAsync(OrdemDeServico ordem, CancellationToken ct);
    Task SalvarAsync(CancellationToken ct);
    Task EmTransacaoSerializadaAsync(Func<CancellationToken, Task> acao, CancellationToken ct);
    Task<MetricaTempoMedio> ObterTempoMedioExecucaoAsync(CancellationToken ct);
    void MarcarItemServicoComoNovo(ItemServico item);
    void MarcarItemPecaComoNovo(ItemPeca item);
}
