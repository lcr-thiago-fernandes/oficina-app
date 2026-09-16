using System.Security.Claims;

namespace Oficina.Api.Configuracao;

/// <summary>
/// Leitura tipada das claims emitidas pela função serverless oficina-auth-api.
/// O contrato do token é fixo e compartilhado com o repositório oficina-lambda-auth.
/// </summary>
///
/// <remarks>
/// DECISÃO — o identificador de propriedade da OS é <c>documento</c>, não <c>sub</c>.
/// A especificação cita <c>sub</c>, mas nesta API a autorização por propriedade é
/// resolvida inteiramente por <c>documento</c> (CPF), e isso é deliberado:
/// <list type="bullet">
///   <item><description>o token de Cliente é emitido A PARTIR do CPF — é o dado que
///   oficina-auth-api tem em mãos e o único que identifica o mesmo sujeito nos dois
///   lados sem um lookup extra;</description></item>
///   <item><description><c>sub</c> não tem significado uniforme entre perfis: num token
///   de Cliente seria o Id do <c>Cliente</c>, num token de Admin/Atendente o Id do
///   <c>Usuario</c> — tabelas diferentes. Autorizar por ele exigiria saber o perfil
///   antes de saber o que o identificador significa;</description></item>
///   <item><description><c>documento</c> já é chave única e indexada em <c>cliente</c>,
///   então a consulta por propriedade não fica mais cara.</description></item>
/// </list>
/// <c>sub</c> continua no token e é informativo (rastreabilidade em log/APM). Por isso
/// NÃO existe aqui um leitor de <c>sub</c>: o que existia (<c>IdDoSujeito</c>) nunca foi
/// chamado por código de produção — só pelos próprios testes — e foi removido em vez de
/// mantido como API que parece suportada.
/// </remarks>
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

    public static string? PerfilDoUsuario(this ClaimsPrincipal usuario) =>
        usuario.FindFirst(ClaimPerfil)?.Value;
}
