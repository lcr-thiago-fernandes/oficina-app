using Microsoft.Extensions.DependencyInjection;
using Oficina.Aplicacao.OrdensServico.Gateways;

namespace Oficina.Infraestrutura.Notificacoes;

public static class DependencyInjectionNotificacoes
{
    public static IServiceCollection AdicionarNotificacoes(this IServiceCollection services)
    {
        services.AddScoped<INotificacaoGateway, NotificacaoEmailMock>();
        return services;
    }
}
