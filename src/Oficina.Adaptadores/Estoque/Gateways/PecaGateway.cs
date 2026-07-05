using Oficina.Adaptadores.Estoque.DataSources;
using Oficina.Aplicacao.Estoque.Gateways;
using Oficina.Dominio.Estoque;

namespace Oficina.Adaptadores.Estoque.Gateways;

public class PecaGateway : IPecaGateway
{
    private readonly IPecaDataSource _dataSource;
    public PecaGateway(IPecaDataSource dataSource) => _dataSource = dataSource;

    public Task<Peca?> ObterPorIdAsync(Guid id, CancellationToken ct) =>
        _dataSource.ObterPorIdAsync(id, ct);

    public Task<Peca?> ObterPorSkuAsync(Sku sku, CancellationToken ct) =>
        _dataSource.ObterPorSkuAsync(sku, ct);

    public Task<bool> ExisteSkuAsync(Sku sku, CancellationToken ct) =>
        _dataSource.ExisteSkuAsync(sku, ct);

    public Task<IReadOnlyList<Peca>> ListarAsync(string? filtroNome, int pagina, int tamanhoPagina, bool incluirInativos, CancellationToken ct) =>
        _dataSource.ListarAsync(filtroNome, pagina, tamanhoPagina, incluirInativos, ct);

    public Task<int> ContarAsync(string? filtroNome, bool incluirInativos, CancellationToken ct) =>
        _dataSource.ContarAsync(filtroNome, incluirInativos, ct);

    public Task AdicionarAsync(Peca peca, CancellationToken ct) =>
        _dataSource.AdicionarAsync(peca, ct);

    public Task SalvarAsync(CancellationToken ct) =>
        _dataSource.SalvarAsync(ct);

    public Task EmTransacaoSerializadaAsync(Func<CancellationToken, Task> acao, CancellationToken ct) =>
        _dataSource.EmTransacaoSerializadaAsync(acao, ct);

    public void MarcarMovimentacaoComoNova(MovimentacaoEstoque movimentacao) =>
        _dataSource.MarcarMovimentacaoComoNova(movimentacao);
}
