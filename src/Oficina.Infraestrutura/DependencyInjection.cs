using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Oficina.Aplicacao.OrdensServico.Saga.Telemetria;
using Oficina.Aplicacao.OrdensServico.Telemetria;
using Oficina.Infraestrutura.Auth;
using Oficina.Infraestrutura.Notificacoes;
using Oficina.Infraestrutura.Persistencia;
using Oficina.Infraestrutura.Telemetria;

namespace Oficina.Infraestrutura;

public static class DependencyInjection
{
    public static IServiceCollection AdicionarInfraestrutura(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("ConnectionStrings:Default não configurada.");

        // Interceptadores do SaveChanges registrados no DI entram no contexto (nenhum em produção; os testes
        // de integração registram um para simular falhas de banco sem duplicar esta configuração).
        services.AddDbContext<OficinaDbContext>((sp, options) =>
            options.UseNpgsql(connectionString, npgsql =>
                    npgsql.MigrationsHistoryTable("__migrations_history", "public"))
                .AddInterceptors(sp.GetServices<ISaveChangesInterceptor>()));

        // Reutilizável pelo startup (HostedService) e pelo Job de migração (Program "migrate").
        // Singleton stateless: cria o próprio escopo a partir do IServiceProvider recebido.
        services.AddSingleton<IInicializadorBanco, InicializadorBanco>();

        services.AdicionarAutenticacao(configuration);
        services.AdicionarRepositorios();
        services.AdicionarNotificacoes();
        services.AddSingleton<IPublicadorEventoOs, PublicadorEventoOsNewRelic>();
        services.AddSingleton<IPublicadorEventoSaga, PublicadorEventoSagaNewRelic>();

        return services;
    }
}
