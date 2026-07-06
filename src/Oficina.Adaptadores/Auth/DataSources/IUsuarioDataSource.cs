using Oficina.Dominio.Auth;

namespace Oficina.Adaptadores.Auth.DataSources;

public interface IUsuarioDataSource
{
    Task<Usuario?> ObterPorUsernameAsync(Username username, CancellationToken cancellationToken);
    Task<bool> ExisteAsync(Username username, CancellationToken cancellationToken);
    Task AdicionarAsync(Usuario usuario, CancellationToken cancellationToken);
    Task SalvarAsync(CancellationToken cancellationToken);
}
