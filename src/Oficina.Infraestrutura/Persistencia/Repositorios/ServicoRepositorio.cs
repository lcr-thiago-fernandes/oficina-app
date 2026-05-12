using Microsoft.EntityFrameworkCore;
using Oficina.Dominio.Catalogo;

namespace Oficina.Infraestrutura.Persistencia.Repositorios;

public class ServicoRepositorio : IServicoRepositorio
{
    private readonly OficinaDbContext _db;

    public ServicoRepositorio(OficinaDbContext db) => _db = db;

    public Task<Servico?> ObterPorIdAsync(Guid id, CancellationToken ct) =>
        _db.Servicos.FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<IReadOnlyList<Servico>> ListarAsync(
        string? filtroNome, int pagina, int tamanhoPagina, bool incluirInativos, CancellationToken ct)
    {
        if (pagina < 1) pagina = 1;
        if (tamanhoPagina < 1 || tamanhoPagina > 100) tamanhoPagina = 20;

        var query = _db.Servicos.AsQueryable();
        if (!incluirInativos)
            query = query.Where(s => s.Ativo);
        if (!string.IsNullOrWhiteSpace(filtroNome))
            query = query.Where(s => EF.Functions.ILike(s.Nome, $"%{filtroNome}%"));

        return await query
            .OrderBy(s => s.Nome)
            .Skip((pagina - 1) * tamanhoPagina)
            .Take(tamanhoPagina)
            .ToListAsync(ct);
    }

    public Task<int> ContarAsync(string? filtroNome, bool incluirInativos, CancellationToken ct)
    {
        var query = _db.Servicos.AsQueryable();
        if (!incluirInativos)
            query = query.Where(s => s.Ativo);
        if (!string.IsNullOrWhiteSpace(filtroNome))
            query = query.Where(s => EF.Functions.ILike(s.Nome, $"%{filtroNome}%"));
        return query.CountAsync(ct);
    }

    public async Task AdicionarAsync(Servico servico, CancellationToken ct) =>
        await _db.Servicos.AddAsync(servico, ct);

    public Task SalvarAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);
}
