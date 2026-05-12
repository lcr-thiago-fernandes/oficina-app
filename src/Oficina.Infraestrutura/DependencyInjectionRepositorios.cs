using Microsoft.Extensions.DependencyInjection;
using Oficina.Dominio.Auth;
using Oficina.Dominio.Catalogo;
using Oficina.Dominio.Clientes;
using Oficina.Dominio.Estoque;
using Oficina.Dominio.OrdensServico;
using Oficina.Infraestrutura.Persistencia.Repositorios;

namespace Oficina.Infraestrutura;

public static class DependencyInjectionRepositorios
{
    public static IServiceCollection AdicionarRepositorios(this IServiceCollection services)
    {
        services.AddScoped<IUsuarioRepositorio, UsuarioRepositorio>();
        services.AddScoped<IClienteRepositorio, ClienteRepositorio>();
        services.AddScoped<IServicoRepositorio, ServicoRepositorio>();
        services.AddScoped<IPecaRepositorio, PecaRepositorio>();
        services.AddScoped<IOrdemDeServicoRepositorio, OrdemDeServicoRepositorio>();
        return services;
    }
}
