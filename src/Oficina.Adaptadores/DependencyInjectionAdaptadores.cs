using Microsoft.Extensions.DependencyInjection;

namespace Oficina.Adaptadores;

public static class DependencyInjectionAdaptadores
{
    public static IServiceCollection AdicionarAdaptadores(this IServiceCollection services)
    {
        // Registros de Controllers de aplicação e Gateways por contexto (preenchidos por contexto).
        return services;
    }
}
