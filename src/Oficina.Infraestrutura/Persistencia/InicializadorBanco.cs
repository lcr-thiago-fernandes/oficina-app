using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Oficina.Aplicacao.Auth;

namespace Oficina.Infraestrutura.Persistencia;

/// <summary>
/// Aplica as migrations pendentes e, se houver senha de bootstrap configurada,
/// cria o usuário admin inicial. Reutilizado tanto pelo startup do host
/// (compose/local) quanto pelo Job de migração do Kubernetes.
/// </summary>
public interface IInicializadorBanco
{
    Task ExecutarAsync(IServiceProvider sp, IConfiguration config, ILogger log, CancellationToken ct);
}

public sealed class InicializadorBanco : IInicializadorBanco
{
    /// <summary>
    /// Indica se a inicialização (migração + bootstrap) deve ocorrer no startup do host.
    /// Sem a chave (compose/local/testes) → true. Em Kubernetes o ConfigMap define
    /// "Bootstrap:ExecutarNoStartup" = "false" para que os pods do Deployment NÃO migrem
    /// (a migração é feita pelo Job dedicado). Valor inválido cai no default true.
    /// </summary>
    public static bool DeveExecutarNoStartup(IConfiguration config)
    {
        var valor = config["Bootstrap:ExecutarNoStartup"];
        return string.IsNullOrWhiteSpace(valor)
            || !bool.TryParse(valor, out var habilitado)
            || habilitado;
    }

    public async Task ExecutarAsync(
        IServiceProvider sp,
        IConfiguration config,
        ILogger log,
        CancellationToken ct)
    {
        using var scope = sp.CreateScope();
        var services = scope.ServiceProvider;

        var db = services.GetRequiredService<OficinaDbContext>();
        log.LogInformation("Aplicando migrations...");
        await db.Database.MigrateAsync(ct);

        var senha = config["AdminBootstrap:Password"];
        if (string.IsNullOrWhiteSpace(senha))
        {
            log.LogWarning("AdminBootstrap:Password não configurado — bootstrap pulado.");
            return;
        }

        var bootstrap = services.GetRequiredService<BootstrapAdminUseCase>();
        await bootstrap.ExecutarAsync(senha, ct);
    }
}
