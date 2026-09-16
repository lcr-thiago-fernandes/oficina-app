using Oficina.Adaptadores.OrdensServico.DataSources;
using Oficina.Aplicacao.OrdensServico.Gateways;
using Oficina.Dominio.OrdensServico;

namespace Oficina.Adaptadores.OrdensServico.Gateways;

public class OrdemDeServicoGateway : IOrdemDeServicoGateway
{
    private readonly IOrdemDeServicoDataSource _dataSource;
    public OrdemDeServicoGateway(IOrdemDeServicoDataSource dataSource) => _dataSource = dataSource;

    public Task<OrdemDeServico?> ObterPorIdAsync(Guid id, CancellationToken ct) =>
        _dataSource.ObterPorIdAsync(id, ct);

    public Task<OrdemDeServico?> ObterPorNumeroAsync(long numero, CancellationToken ct) =>
        _dataSource.ObterPorNumeroAsync(numero, ct);

    public Task<IReadOnlyList<OrdemDeServico>> ListarAsync(StatusOrdemDeServico? statusFiltro, int pagina, int tamanhoPagina, CancellationToken ct) =>
        _dataSource.ListarAsync(statusFiltro, pagina, tamanhoPagina, ct);

    public Task<IReadOnlyList<OrdemDeServico>> ListarPorClienteAsync(
        Guid clienteId, CancellationToken ct) => _dataSource.ListarPorClienteAsync(clienteId, ct);

    public Task<int> ContarAsync(StatusOrdemDeServico? statusFiltro, CancellationToken ct) =>
        _dataSource.ContarAsync(statusFiltro, ct);

    public Task AdicionarAsync(OrdemDeServico ordem, CancellationToken ct) =>
        _dataSource.AdicionarAsync(ordem, ct);

    public Task SalvarAsync(CancellationToken ct) =>
        _dataSource.SalvarAsync(ct);

    public Task EmTransacaoSerializadaAsync(Func<CancellationToken, Task> acao, CancellationToken ct) =>
        _dataSource.EmTransacaoSerializadaAsync(acao, ct);

    public Task<MetricaTempoMedio> ObterTempoMedioExecucaoAsync(CancellationToken ct) =>
        _dataSource.ObterTempoMedioExecucaoAsync(ct);

    public void MarcarItemServicoComoNovo(ItemServico item) =>
        _dataSource.MarcarItemServicoComoNovo(item);

    public void MarcarItemPecaComoNovo(ItemPeca item) =>
        _dataSource.MarcarItemPecaComoNovo(item);
}
