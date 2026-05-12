using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Oficina.Dominio.Estoque;

namespace Oficina.Infraestrutura.Persistencia.Repositorios;

public class PecaRepositorio : IPecaRepositorio
{
    private readonly OficinaDbContext _db;

    public PecaRepositorio(OficinaDbContext db) => _db = db;

    public Task<Peca?> ObterPorIdAsync(Guid id, CancellationToken ct) =>
        _db.Pecas.Include(p => p.Movimentacoes).FirstOrDefaultAsync(p => p.Id == id, ct);

    public Task<Peca?> ObterPorSkuAsync(Sku sku, CancellationToken ct)
    {
        var v = sku.Valor;
        return _db.Pecas.Include(p => p.Movimentacoes)
            .FirstOrDefaultAsync(p => p.Sku.Valor == v, ct);
    }

    public Task<bool> ExisteSkuAsync(Sku sku, CancellationToken ct)
    {
        var v = sku.Valor;
        return _db.Pecas.AnyAsync(p => p.Sku.Valor == v, ct);
    }

    public async Task<IReadOnlyList<Peca>> ListarAsync(
        string? filtroNome, int pagina, int tamanhoPagina, bool incluirInativos, CancellationToken ct)
    {
        if (pagina < 1) pagina = 1;
        if (tamanhoPagina < 1 || tamanhoPagina > 100) tamanhoPagina = 20;

        var q = _db.Pecas.AsQueryable();
        if (!incluirInativos) q = q.Where(p => p.Ativo);
        if (!string.IsNullOrWhiteSpace(filtroNome))
            q = q.Where(p => EF.Functions.ILike(p.Nome, $"%{filtroNome}%"));

        return await q
            .OrderBy(p => p.Nome)
            .Skip((pagina - 1) * tamanhoPagina)
            .Take(tamanhoPagina)
            .ToListAsync(ct);
    }

    public Task<int> ContarAsync(string? filtroNome, bool incluirInativos, CancellationToken ct)
    {
        var q = _db.Pecas.AsQueryable();
        if (!incluirInativos) q = q.Where(p => p.Ativo);
        if (!string.IsNullOrWhiteSpace(filtroNome))
            q = q.Where(p => EF.Functions.ILike(p.Nome, $"%{filtroNome}%"));
        return q.CountAsync(ct);
    }

    public async Task AdicionarAsync(Peca peca, CancellationToken ct) =>
        await _db.Pecas.AddAsync(peca, ct);

    public Task SalvarAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);

    public async Task EmTransacaoSerializadaAsync(Func<CancellationToken, Task> acao, CancellationToken ct)
    {
        IDbContextTransaction? tx = null;
        try
        {
            tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            await acao(ct);
            await tx.CommitAsync(ct);
        }
        catch
        {
            if (tx is not null) await tx.RollbackAsync(ct);
            throw;
        }
        finally
        {
            if (tx is not null) await tx.DisposeAsync();
        }
    }
}
