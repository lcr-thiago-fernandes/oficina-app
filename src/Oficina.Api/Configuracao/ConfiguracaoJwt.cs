using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Oficina.Infraestrutura.Auth;

namespace Oficina.Api.Configuracao;

public static class ConfiguracaoJwt
{
    public static IServiceCollection AdicionarJwtBearer(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var jwt = configuration.GetSection(JwtOptions.Secao).Get<JwtOptions>()
            ?? throw new InvalidOperationException("Seção 'Jwt' ausente.");

        if (string.IsNullOrWhiteSpace(jwt.Secret) || jwt.Secret.Length < 32)
            throw new InvalidOperationException("Jwt.Secret precisa ter pelo menos 32 caracteres.");

        var chave = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Secret));

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.RequireHttpsMetadata = false; // dev/MVP; prod = true
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = chave,
                    ClockSkew = TimeSpan.FromSeconds(30)
                };
            });

        return services;
    }
}
