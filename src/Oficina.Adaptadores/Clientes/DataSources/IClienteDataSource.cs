using Oficina.Dominio.Clientes;

namespace Oficina.Adaptadores.Clientes.DataSources;

public interface IClienteDataSource
{
    Task<Cliente?> ObterPorIdAsync(Guid id, CancellationToken ct);
    Task<Cliente?> ObterPorDocumentoAsync(Documento documento, CancellationToken ct);
    Task<bool> ExisteDocumentoAsync(Documento documento, CancellationToken ct);
    Task<IReadOnlyList<Cliente>> ListarAsync(int pagina, int tamanhoPagina, CancellationToken ct);
    Task<int> ContarAsync(CancellationToken ct);
    Task AdicionarAsync(Cliente cliente, CancellationToken ct);
    Task SalvarAsync(CancellationToken ct);
    void Remover(Cliente cliente);
    void MarcarVeiculoComoNovo(Veiculo veiculo);
}
