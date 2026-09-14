using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Oficina.Infraestrutura.Auth;

public static class DependencyInjectionAuth
{
    public static IServiceCollection AdicionarAutenticacao(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Somente parâmetros de VALIDAÇÃO do token. A emissão é da função
        // serverless oficina-auth-api, em repositório separado.
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.Secao));

        services.AddHostedService<BootstrapAdminHostedService>();

        return services;
    }
}
