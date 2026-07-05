using Microsoft.Extensions.DependencyInjection;
using Oficina.Adaptadores.Catalogo.Controllers;
using Oficina.Adaptadores.Catalogo.Gateways;
using Oficina.Aplicacao.Catalogo.Gateways;

namespace Oficina.Adaptadores;

public static class DependencyInjectionAdaptadores
{
    public static IServiceCollection AdicionarAdaptadores(this IServiceCollection services)
    {
        // Catálogo
        services.AddScoped<IServicoGateway, ServicoGateway>();
        services.AddScoped<ServicoController>();
        return services;
    }
}
