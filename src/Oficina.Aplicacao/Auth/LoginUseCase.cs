using Microsoft.Extensions.Logging;
using Oficina.Dominio.Auth;
using Oficina.Dominio.ServicosCompartilhados;

namespace Oficina.Aplicacao.Auth;

public class LoginUseCase
{
    private readonly IUsuarioRepositorio _repo;
    private readonly IGeradorTokenJwt _gerador;
    private readonly ILogger<LoginUseCase> _log;

    public LoginUseCase(
        IUsuarioRepositorio repo,
        IGeradorTokenJwt gerador,
        ILogger<LoginUseCase> log)
    {
        _repo = repo;
        _gerador = gerador;
        _log = log;
    }

    public async Task<ResultadoLogin> ExecutarAsync(LoginRequest req, CancellationToken ct)
    {
        Username username;
        try
        {
            username = Username.Criar(req.Username);
        }
        catch (ArgumentException)
        {
            // username inválido — não vaza qual foi o erro
            return new ResultadoLogin.CredenciaisInvalidas();
        }

        var usuario = await _repo.ObterPorUsernameAsync(username, ct);
        if (usuario is null)
        {
            _log.LogWarning("Tentativa de login com username inexistente.");
            return new ResultadoLogin.CredenciaisInvalidas();
        }

        if (!usuario.Ativo)
            return new ResultadoLogin.UsuarioInativo();

        if (!usuario.Autenticar(req.Password))
        {
            _log.LogWarning("Tentativa de login falhou para {Username}.", username.Valor);
            return new ResultadoLogin.CredenciaisInvalidas();
        }

        var token = _gerador.Gerar(usuario);
        return new ResultadoLogin.Sucesso(
            new LoginResponse(token.AccessToken, token.ExpiraEmSegundos, usuario.PrecisaTrocarSenha));
    }
}
