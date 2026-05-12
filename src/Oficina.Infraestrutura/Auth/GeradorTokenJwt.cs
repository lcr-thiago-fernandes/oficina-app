using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Oficina.Dominio.Auth;
using Oficina.Dominio.ServicosCompartilhados;

namespace Oficina.Infraestrutura.Auth;

public class GeradorTokenJwt : IGeradorTokenJwt
{
    private readonly JwtOptions _options;

    public GeradorTokenJwt(IOptions<JwtOptions> options) => _options = options.Value;

    public TokenJwt Gerar(Usuario usuario)
    {
        var chave = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Secret));
        var credentials = new SigningCredentials(chave, SecurityAlgorithms.HmacSha256);
        var expira = DateTime.UtcNow.AddMinutes(_options.ExpiracaoEmMinutos);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, usuario.Id.ToString()),
            new(JwtRegisteredClaimNames.UniqueName, usuario.Username.Valor),
            new("perfil", usuario.Perfil.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expira,
            signingCredentials: credentials);

        var jwt = new JwtSecurityTokenHandler().WriteToken(token);
        return new TokenJwt(jwt, _options.ExpiracaoEmMinutos * 60);
    }
}
