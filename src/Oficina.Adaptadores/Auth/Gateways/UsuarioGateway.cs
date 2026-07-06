using Oficina.Adaptadores.Auth.DataSources;
using Oficina.Aplicacao.Auth.Gateways;
using Oficina.Dominio.Auth;

namespace Oficina.Adaptadores.Auth.Gateways;

public class UsuarioGateway : IUsuarioGateway
{
    private readonly IUsuarioDataSource _dataSource;
    public UsuarioGateway(IUsuarioDataSource dataSource) => _dataSource = dataSource;

    public Task<Usuario?> ObterPorUsernameAsync(Username username, CancellationToken ct) =>
        _dataSource.ObterPorUsernameAsync(username, ct);

    public Task<bool> ExisteAsync(Username username, CancellationToken ct) =>
        _dataSource.ExisteAsync(username, ct);

    public Task AdicionarAsync(Usuario usuario, CancellationToken ct) =>
        _dataSource.AdicionarAsync(usuario, ct);

    public Task SalvarAsync(CancellationToken ct) =>
        _dataSource.SalvarAsync(ct);
}
