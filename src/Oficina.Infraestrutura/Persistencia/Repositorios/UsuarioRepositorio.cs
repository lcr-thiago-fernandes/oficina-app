using Microsoft.EntityFrameworkCore;
using Oficina.Dominio.Auth;

namespace Oficina.Infraestrutura.Persistencia.Repositorios;

public class UsuarioRepositorio : IUsuarioRepositorio
{
    private readonly OficinaDbContext _db;

    public UsuarioRepositorio(OficinaDbContext db) => _db = db;

    public Task<Usuario?> ObterPorUsernameAsync(Username username, CancellationToken ct)
    {
        var valor = username.Valor;
        return _db.Usuarios
            .FirstOrDefaultAsync(u => u.Username.Valor == valor, ct);
    }

    public Task<bool> ExisteAsync(Username username, CancellationToken ct)
    {
        var valor = username.Valor;
        return _db.Usuarios.AnyAsync(u => u.Username.Valor == valor, ct);
    }

    public async Task AdicionarAsync(Usuario usuario, CancellationToken ct)
    {
        await _db.Usuarios.AddAsync(usuario, ct);
    }

    public Task SalvarAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);
}
