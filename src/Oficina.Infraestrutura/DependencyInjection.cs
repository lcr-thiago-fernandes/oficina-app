using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Oficina.Infraestrutura.Auth;
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

        services.AdicionarAutenticacao(configuration);
        services.AdicionarRepositorios();

        return services;
    }
}
