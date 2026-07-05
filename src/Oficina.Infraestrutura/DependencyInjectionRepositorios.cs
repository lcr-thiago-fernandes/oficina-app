using Microsoft.Extensions.DependencyInjection;
using Oficina.Adaptadores.Catalogo.DataSources;
using Oficina.Adaptadores.Clientes.DataSources;
using Oficina.Dominio.Auth;
using Oficina.Dominio.Estoque;
using Oficina.Dominio.OrdensServico;
using Oficina.Infraestrutura.Persistencia.DataSources;
using Oficina.Infraestrutura.Persistencia.Repositorios;

namespace Oficina.Infraestrutura;

public static class DependencyInjectionRepositorios
{
    public static IServiceCollection AdicionarRepositorios(this IServiceCollection services)
    {
        services.AddScoped<IUsuarioRepositorio, UsuarioRepositorio>();
        services.AddScoped<IPecaRepositorio, PecaRepositorio>();
        services.AddScoped<IOrdemDeServicoRepositorio, OrdemDeServicoRepositorio>();

        // DataSources (Clean Architecture — Frameworks & Drivers)
        services.AddScoped<IServicoDataSource, ServicoDataSource>();
        services.AddScoped<IClienteDataSource, ClienteDataSource>();
        return services;
    }
}
