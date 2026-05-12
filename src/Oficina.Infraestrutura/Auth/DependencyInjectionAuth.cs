using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Oficina.Dominio.ServicosCompartilhados;

namespace Oficina.Infraestrutura.Auth;

public static class DependencyInjectionAuth
{
    public static IServiceCollection AdicionarAutenticacao(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.Secao));

        services.AddSingleton<IGeradorTokenJwt, GeradorTokenJwt>();

        services.AddHostedService<BootstrapAdminHostedService>();

        return services;
    }
}
