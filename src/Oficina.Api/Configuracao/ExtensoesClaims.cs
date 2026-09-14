using System.Security.Claims;

namespace Oficina.Api.Configuracao;

/// <summary>
/// Leitura tipada das claims emitidas pela Lambda de autenticação.
/// O contrato do token é fixo e compartilhado com o repositório oficina-lambda-auth.
/// </summary>
public static class ExtensoesClaims
{
    public const string ClaimDocumento = "documento";
    public const string ClaimPerfil = "perfil";

    /// <summary>CPF do cliente autenticado. Nulo em tokens de Admin/Atendente.</summary>
    public static string? DocumentoDoCliente(this ClaimsPrincipal usuario)
    {
        var valor = usuario.FindFirst(ClaimDocumento)?.Value;
        return string.IsNullOrWhiteSpace(valor) ? null : valor;
    }

    /// <summary>Identificador do sujeito do token (cliente ou usuário administrativo).</summary>
    public static Guid? IdDoSujeito(this ClaimsPrincipal usuario)
    {
        // JwtBearer mapeia "sub" para ClaimTypes.NameIdentifier por padrao;
        // aceitamos as duas formas para nao depender do mapeamento.
        var valor = usuario.FindFirst("sub")?.Value
                    ?? usuario.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        return Guid.TryParse(valor, out var id) ? id : null;
    }

    public static string? PerfilDoUsuario(this ClaimsPrincipal usuario) =>
        usuario.FindFirst(ClaimPerfil)?.Value;
}
