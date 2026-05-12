namespace Oficina.Dominio.Estoque;

public interface IPecaRepositorio
{
    Task<Peca?> ObterPorIdAsync(Guid id, CancellationToken ct);
    Task<Peca?> ObterPorSkuAsync(Sku sku, CancellationToken ct);
    Task<bool> ExisteSkuAsync(Sku sku, CancellationToken ct);
    Task<IReadOnlyList<Peca>> ListarAsync(string? filtroNome, int pagina, int tamanhoPagina, bool incluirInativos, CancellationToken ct);
    Task<int> ContarAsync(string? filtroNome, bool incluirInativos, CancellationToken ct);
    Task AdicionarAsync(Peca peca, CancellationToken ct);
    Task SalvarAsync(CancellationToken ct);

    /// <summary>
    /// Executa uma operação em transação serializável (para garantir
    /// consistência de saldo sob concorrência).
    /// </summary>
    Task EmTransacaoSerializadaAsync(Func<CancellationToken, Task> acao, CancellationToken ct);

    // EF Core nao detecta entidades adicionadas via navigation collection
    // como Added quando o Id ja vem preenchido — gera UPDATE em vez de
    // INSERT. Marca a movimentacao como Added explicitamente.
    void MarcarMovimentacaoComoNova(MovimentacaoEstoque movimentacao);
}
