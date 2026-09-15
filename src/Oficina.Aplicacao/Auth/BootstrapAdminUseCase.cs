using Microsoft.Extensions.Logging;
using Oficina.Aplicacao.Auth.Gateways;
using Oficina.Dominio.Auth;

namespace Oficina.Aplicacao.Auth;

/// <summary>
/// Cria o usuário administrativo inicial em <c>auth.usuario</c>.
/// </summary>
/// <remarks>
/// CONTRATO ENTRE REPOSITÓRIOS — não é código morto, não remova.
/// Esta API escreve o hash BCrypt do admin em <c>auth.usuario</c> e NÃO tem nenhum
/// endpoint que o verifique: desde a Fase 3 ela não emite token. Quem lê essa tabela
/// e compara a senha é a função serverless <c>oficina-auth-api</c> (repositório
/// <c>oficina-lambda-auth</c>), no fluxo de login por usuário/senha de
/// Atendente/Admin. Por isso <see cref="Oficina.Dominio.Auth.Usuario.Autenticar"/> e
/// <c>ObterPorUsernameAsync</c> (gateway, data source, interfaces) continuam aqui sem
/// chamador local: eles definem e sustentam o formato do dado que o outro repositório
/// consome — trocar o algoritmo de hash, o nome da coluna ou a tabela quebra o login
/// lá, silenciosamente, sem quebrar nenhum teste daqui.
/// </remarks>
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
