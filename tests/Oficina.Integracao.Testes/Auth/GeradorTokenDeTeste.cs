using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Oficina.Integracao.Testes.Auth;

/// <summary>
/// Assina tokens localmente nos testes. Em produção quem emite é a Lambda
/// oficina-auth-api; a API apenas valida. Os valores de secret/issuer/audience
/// precisam bater com os definidos em <see cref="AuthFixture"/>.
/// </summary>
public static class GeradorTokenDeTeste
{
    public const string Secret = "chave-de-teste-com-mais-de-32-caracteres-para-hs256";
    public const string Issuer = "oficina-auth";
    public const string Audience = "oficina-api";

    public static string Gerar(string perfil, Guid sub, string? documento = null, string nome = "Teste")
    {
        var chave = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret));
        var credenciais = new SigningCredentials(chave, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new("sub", sub.ToString()),
            new("perfil", perfil),
            new("nome", nome),
            new("jti", Guid.NewGuid().ToString())
        };

        if (documento is not null)
            claims.Add(new Claim("documento", documento));

        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddMinutes(60),
            signingCredentials: credenciais);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
