using Oficina.Adaptadores.Catalogo.DataSources;
using Oficina.Aplicacao.Catalogo.Gateways;
using Oficina.Dominio.Catalogo;

namespace Oficina.Adaptadores.Catalogo.Gateways;

public class ServicoGateway : IServicoGateway
{
    private readonly IServicoDataSource _dataSource;
    public ServicoGateway(IServicoDataSource dataSource) => _dataSource = dataSource;

    public Task<Servico?> ObterPorIdAsync(Guid id, CancellationToken ct) =>
        _dataSource.ObterPorIdAsync(id, ct);

    public Task<IReadOnlyList<Servico>> ListarAsync(string? filtroNome, int pagina, int tamanhoPagina, bool incluirInativos, CancellationToken ct) =>
        _dataSource.ListarAsync(filtroNome, pagina, tamanhoPagina, incluirInativos, ct);

    public Task<int> ContarAsync(string? filtroNome, bool incluirInativos, CancellationToken ct) =>
        _dataSource.ContarAsync(filtroNome, incluirInativos, ct);

    public Task AdicionarAsync(Servico servico, CancellationToken ct) =>
        _dataSource.AdicionarAsync(servico, ct);

    public Task SalvarAsync(CancellationToken ct) =>
        _dataSource.SalvarAsync(ct);
}
