using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Oficina.Infraestrutura.Auth;

public static class DependencyInjectionAuth
{
    public static IServiceCollection AdicionarAutenticacao(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Somente parametros de VALIDACAO do token. A emissao e da funcao
        // serverless oficina-auth, em repositorio separado.
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.Secao));

        services.AddHostedService<BootstrapAdminHostedService>();

        return services;
    }
}
