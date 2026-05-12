using Microsoft.EntityFrameworkCore;
using Oficina.Dominio.Clientes;

namespace Oficina.Infraestrutura.Persistencia.Repositorios;

public class ClienteRepositorio : IClienteRepositorio
{
    private readonly OficinaDbContext _db;

    public ClienteRepositorio(OficinaDbContext db) => _db = db;

    public Task<Cliente?> ObterPorIdAsync(Guid id, CancellationToken ct) =>
        _db.Clientes.Include(c => c.Veiculos).FirstOrDefaultAsync(c => c.Id == id, ct);

    public Task<Cliente?> ObterPorDocumentoAsync(Documento documento, CancellationToken ct)
    {
        var v = documento.Valor;
        return _db.Clientes.Include(c => c.Veiculos)
            .FirstOrDefaultAsync(c => c.Documento.Valor == v, ct);
    }

    public Task<bool> ExisteDocumentoAsync(Documento documento, CancellationToken ct)
    {
        var v = documento.Valor;
        return _db.Clientes.AnyAsync(c => c.Documento.Valor == v, ct);
    }

    public async Task<IReadOnlyList<Cliente>> ListarAsync(int pagina, int tamanhoPagina, CancellationToken ct)
    {
        if (pagina < 1) pagina = 1;
        if (tamanhoPagina < 1 || tamanhoPagina > 100) tamanhoPagina = 20;

        return await _db.Clientes
            .Include(c => c.Veiculos)
            .Where(c => c.Ativo)
            .OrderBy(c => c.Nome)
            .Skip((pagina - 1) * tamanhoPagina)
            .Take(tamanhoPagina)
            .ToListAsync(ct);
    }

    public Task<int> ContarAsync(CancellationToken ct) =>
        _db.Clientes.CountAsync(c => c.Ativo, ct);

    public async Task AdicionarAsync(Cliente cliente, CancellationToken ct) =>
        await _db.Clientes.AddAsync(cliente, ct);

    public Task SalvarAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);

    public void Remover(Cliente cliente) => _db.Clientes.Remove(cliente);

    public void MarcarVeiculoComoNovo(Veiculo veiculo) =>
        _db.Entry(veiculo).State = EntityState.Added;
}
