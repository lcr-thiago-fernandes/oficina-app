using Oficina.Dominio.Catalogo;

namespace Oficina.Adaptadores.Catalogo.DataSources;

public interface IServicoDataSource
{
    Task<Servico?> ObterPorIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<Servico>> ListarAsync(string? filtroNome, int pagina, int tamanhoPagina, bool incluirInativos, CancellationToken ct);
    Task<int> ContarAsync(string? filtroNome, bool incluirInativos, CancellationToken ct);
    Task AdicionarAsync(Servico servico, CancellationToken ct);
    Task SalvarAsync(CancellationToken ct);
}
