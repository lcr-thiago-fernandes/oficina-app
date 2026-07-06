using Microsoft.Extensions.Logging;
using Oficina.Aplicacao.Auth.Gateways;
using Oficina.Dominio.Auth;

namespace Oficina.Aplicacao.Auth;

public class BootstrapAdminUseCase
{
    private readonly IUsuarioGateway _gateway;
    private readonly ILogger<BootstrapAdminUseCase> _log;

    public BootstrapAdminUseCase(IUsuarioGateway gateway, ILogger<BootstrapAdminUseCase> log)
    {
        _gateway = gateway;
        _log = log;
    }

    public async Task ExecutarAsync(string senhaInicial, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(senhaInicial))
            throw new InvalidOperationException(
                "ADMIN_BOOTSTRAP_PASSWORD não configurada — não é possível bootstrap.");

        var username = Username.Criar("admin");

        if (await _gateway.ExisteAsync(username, ct))
        {
            _log.LogInformation("Usuário admin já existe — bootstrap ignorado.");
            return;
        }

        var usuario = Usuario.CriarParaBootstrap(username, Senha.DeTextoPuro(senhaInicial));
        await _gateway.AdicionarAsync(usuario, ct);
        await _gateway.SalvarAsync(ct);

        _log.LogWarning("Usuário admin criado pelo bootstrap. Troca de senha exigida no primeiro login.");
    }
}
