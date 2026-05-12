namespace Oficina.Dominio.Auth;

public interface IUsuarioRepositorio
{
    Task<Usuario?> ObterPorUsernameAsync(Username username, CancellationToken cancellationToken);
    Task<bool> ExisteAsync(Username username, CancellationToken cancellationToken);
    Task AdicionarAsync(Usuario usuario, CancellationToken cancellationToken);
    Task SalvarAsync(CancellationToken cancellationToken);
}
