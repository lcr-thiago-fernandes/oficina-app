using Oficina.Dominio.Auth;

namespace Oficina.Aplicacao.Auth.Gateways;

public interface IUsuarioGateway
{
    Task<Usuario?> ObterPorUsernameAsync(Username username, CancellationToken cancellationToken);
    Task<bool> ExisteAsync(Username username, CancellationToken cancellationToken);
    Task AdicionarAsync(Usuario usuario, CancellationToken cancellationToken);
    Task SalvarAsync(CancellationToken cancellationToken);
}
