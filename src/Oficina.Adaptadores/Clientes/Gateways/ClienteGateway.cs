using Oficina.Adaptadores.Clientes.DataSources;
using Oficina.Aplicacao.Clientes.Gateways;
using Oficina.Dominio.Clientes;

namespace Oficina.Adaptadores.Clientes.Gateways;

public class ClienteGateway : IClienteGateway
{
    private readonly IClienteDataSource _dataSource;
    public ClienteGateway(IClienteDataSource dataSource) => _dataSource = dataSource;

    public Task<Cliente?> ObterPorIdAsync(Guid id, CancellationToken ct) =>
        _dataSource.ObterPorIdAsync(id, ct);

    public Task<Cliente?> ObterPorDocumentoAsync(Documento documento, CancellationToken ct) =>
        _dataSource.ObterPorDocumentoAsync(documento, ct);

    public Task<bool> ExisteDocumentoAsync(Documento documento, CancellationToken ct) =>
        _dataSource.ExisteDocumentoAsync(documento, ct);

    public Task<IReadOnlyList<Cliente>> ListarAsync(int pagina, int tamanhoPagina, CancellationToken ct) =>
        _dataSource.ListarAsync(pagina, tamanhoPagina, ct);

    public Task<int> ContarAsync(CancellationToken ct) =>
        _dataSource.ContarAsync(ct);

    public Task AdicionarAsync(Cliente cliente, CancellationToken ct) =>
        _dataSource.AdicionarAsync(cliente, ct);

    public Task SalvarAsync(CancellationToken ct) =>
        _dataSource.SalvarAsync(ct);

    public void Remover(Cliente cliente) =>
        _dataSource.Remover(cliente);

    public void MarcarVeiculoComoNovo(Veiculo veiculo) =>
        _dataSource.MarcarVeiculoComoNovo(veiculo);
}
