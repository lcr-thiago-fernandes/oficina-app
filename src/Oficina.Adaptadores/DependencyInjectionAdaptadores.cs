using Microsoft.Extensions.DependencyInjection;
using Oficina.Adaptadores.Auth.Gateways;
using Oficina.Adaptadores.Catalogo.Controllers;
using Oficina.Adaptadores.Catalogo.Gateways;
using Oficina.Adaptadores.Clientes.Controllers;
using Oficina.Adaptadores.Clientes.Gateways;
using Oficina.Adaptadores.Estoque.Controllers;
using Oficina.Adaptadores.Estoque.Gateways;
using Oficina.Adaptadores.OrdensServico.Controllers;
using Oficina.Adaptadores.OrdensServico.Gateways;
using Oficina.Aplicacao.Auth.Gateways;
using Oficina.Aplicacao.Catalogo.Gateways;
using Oficina.Aplicacao.Clientes.Gateways;
using Oficina.Aplicacao.Estoque.Gateways;
using Oficina.Aplicacao.OrdensServico.Gateways;

namespace Oficina.Adaptadores;

public static class DependencyInjectionAdaptadores
{
    public static IServiceCollection AdicionarAdaptadores(this IServiceCollection services)
    {
        // Auth — apenas o gateway do usuario administrativo (bootstrap do admin).
        // A API nao emite mais token: quem autentica e a funcao serverless oficina-auth.
        services.AddScoped<IUsuarioGateway, UsuarioGateway>();

        // Catálogo
        services.AddScoped<IServicoGateway, ServicoGateway>();
        services.AddScoped<ServicoController>();

        // Clientes
        services.AddScoped<IClienteGateway, ClienteGateway>();
        services.AddScoped<ClienteController>();

        // Estoque
        services.AddScoped<IPecaGateway, PecaGateway>();
        services.AddScoped<PecaController>();

        // Ordens de Serviço
        services.AddScoped<IOrdemDeServicoGateway, OrdemDeServicoGateway>();
        services.AddScoped<OrdemDeServicoController>();
        return services;
    }
}
