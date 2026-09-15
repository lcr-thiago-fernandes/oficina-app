using Oficina.Dominio.Auth;

namespace Oficina.Aplicacao.Auth.Gateways;

public interface IUsuarioGateway
{
    // Sem chamador nesta API: a leitura por username serve ao login de
    // usuário/senha, que roda na função serverless oficina-auth-api. Mantido como
    // parte do contrato de auth.usuario — ver BootstrapAdminUseCase.
    Task<Usuario?> ObterPorUsernameAsync(Username username, CancellationToken cancellationToken);
    Task<bool> ExisteAsync(Username username, CancellationToken cancellationToken);
    Task AdicionarAsync(Usuario usuario, CancellationToken cancellationToken);
    Task SalvarAsync(CancellationToken cancellationToken);
}
