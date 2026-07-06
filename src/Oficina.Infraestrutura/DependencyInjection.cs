using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Oficina.Infraestrutura.Auth;
using Oficina.Infraestrutura.Notificacoes;
using Oficina.Infraestrutura.Persistencia;

namespace Oficina.Infraestrutura;

public static class DependencyInjection
{
    public static IServiceCollection AdicionarInfraestrutura(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("ConnectionStrings:Default não configurada.");

        services.AddDbContext<OficinaDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsHistoryTable("__migrations_history", "public")));

        // Reutilizável pelo startup (HostedService) e pelo Job de migração (Program "migrate").
        // Singleton stateless: cria o próprio escopo a partir do IServiceProvider recebido.
        services.AddSingleton<IInicializadorBanco, InicializadorBanco>();

        services.AdicionarAutenticacao(configuration);
        services.AdicionarRepositorios();
        services.AdicionarNotificacoes();

        return services;
    }
}
