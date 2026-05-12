using Microsoft.EntityFrameworkCore;
using Oficina.Dominio.Auth;

namespace Oficina.Infraestrutura.Persistencia.Repositorios;

public class UsuarioRepositorio : IUsuarioRepositorio
{
    private readonly OficinaDbContext _db;

    public UsuarioRepositorio(OficinaDbContext db) => _db = db;

    public Task<Usuario?> ObterPorUsernameAsync(Username username, CancellationToken ct)
    {
        // Username é mapeado com HasConversion: EF traduz a comparação direta
        // u.Username == username usando o converter (string ↔ Username).
        // Acessar .Valor não é traduzível porque a propriedade some no SQL.
        return _db.Usuarios.FirstOrDefaultAsync(u => u.Username == username, ct);
    }

    public Task<bool> ExisteAsync(Username username, CancellationToken ct)
    {
        return _db.Usuarios.AnyAsync(u => u.Username == username, ct);
    }

    public async Task AdicionarAsync(Usuario usuario, CancellationToken ct)
    {
        await _db.Usuarios.AddAsync(usuario, ct);
    }

    public Task SalvarAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);
}
