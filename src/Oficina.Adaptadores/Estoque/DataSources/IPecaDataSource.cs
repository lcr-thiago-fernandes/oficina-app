using Oficina.Dominio.Estoque;

namespace Oficina.Adaptadores.Estoque.DataSources;

public interface IPecaDataSource
{
    Task<Peca?> ObterPorIdAsync(Guid id, CancellationToken ct);
    Task<Peca?> ObterPorSkuAsync(Sku sku, CancellationToken ct);
    Task<bool> ExisteSkuAsync(Sku sku, CancellationToken ct);
    Task<IReadOnlyList<Peca>> ListarAsync(string? filtroNome, int pagina, int tamanhoPagina, bool incluirInativos, CancellationToken ct);
    Task<int> ContarAsync(string? filtroNome, bool incluirInativos, CancellationToken ct);
    Task AdicionarAsync(Peca peca, CancellationToken ct);
    Task SalvarAsync(CancellationToken ct);
    Task EmTransacaoSerializadaAsync(Func<CancellationToken, Task> acao, CancellationToken ct);
    void MarcarMovimentacaoComoNova(MovimentacaoEstoque movimentacao);
}
