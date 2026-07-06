using Microsoft.Extensions.DependencyInjection;
using Oficina.Adaptadores.Auth.DataSources;
using Oficina.Adaptadores.Catalogo.DataSources;
using Oficina.Adaptadores.Clientes.DataSources;
using Oficina.Adaptadores.Estoque.DataSources;
using Oficina.Adaptadores.OrdensServico.DataSources;
using Oficina.Infraestrutura.Persistencia.DataSources;

namespace Oficina.Infraestrutura;

public static class DependencyInjectionRepositorios
{
    public static IServiceCollection AdicionarRepositorios(this IServiceCollection services)
    {
        // DataSources (Clean Architecture — Frameworks & Drivers)
        services.AddScoped<IUsuarioDataSource, UsuarioDataSource>();
        services.AddScoped<IServicoDataSource, ServicoDataSource>();
        services.AddScoped<IClienteDataSource, ClienteDataSource>();
        services.AddScoped<IPecaDataSource, PecaDataSource>();
        services.AddScoped<IOrdemDeServicoDataSource, OrdemDeServicoDataSource>();
        return services;
    }
}
