using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Oficina.Api.Configuracao;
using Oficina.Aplicacao.Auth;

namespace Oficina.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly LoginUseCase _login;

    public AuthController(LoginUseCase login) => _login = login;

    [HttpPost("login")]
    [EnableRateLimiting(ConfiguracaoRateLimit.PoliticaLogin)]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Login([FromBody] LoginRequest req, CancellationToken ct)
    {
        if (req is null || string.IsNullOrWhiteSpace(req.Username) || string.IsNullOrWhiteSpace(req.Password))
            return BadRequest(new { erro = "Username e password são obrigatórios." });

        var resultado = await _login.ExecutarAsync(req, ct);
        return resultado switch
        {
            ResultadoLogin.Sucesso s => Ok(s.Response),
            ResultadoLogin.CredenciaisInvalidas => Unauthorized(new { erro = "Credenciais inválidas." }),
            ResultadoLogin.UsuarioInativo => StatusCode(StatusCodes.Status403Forbidden, new { erro = "Usuário inativo." }),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }
}
