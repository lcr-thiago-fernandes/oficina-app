using Microsoft.AspNetCore.Authorization;

namespace Oficina.Api.Configuracao;

public static class PoliticasDeAutorizacao
{
    public const string RequerAdmin = "RequerAdmin";
    public const string RequerAdminOuAtendente = "RequerAdminOuAtendente";
    public const string RequerCliente = "RequerCliente";

    public static IServiceCollection AdicionarPoliticas(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            options.AddPolicy(RequerAdmin, p =>
                p.RequireAuthenticatedUser().RequireClaim("perfil", "Admin"));

            options.AddPolicy(RequerAdminOuAtendente, p =>
                p.RequireAuthenticatedUser().RequireClaim("perfil", "Admin", "Atendente"));

            options.AddPolicy(RequerCliente, p =>
                p.RequireAuthenticatedUser()
                 .RequireClaim("perfil", "Cliente")
                 .RequireClaim(ExtensoesClaims.ClaimDocumento));
        });
        return services;
    }
}
