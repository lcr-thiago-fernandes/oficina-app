using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Oficina.Aplicacao.Auth;
using Oficina.Infraestrutura.Persistencia;

namespace Oficina.Infraestrutura.Auth;

public class BootstrapAdminHostedService : IHostedService
{
    private readonly IServiceProvider _sp;
    private readonly IConfiguration _config;
    private readonly ILogger<BootstrapAdminHostedService> _log;

    public BootstrapAdminHostedService(
        IServiceProvider sp,
        IConfiguration config,
        ILogger<BootstrapAdminHostedService> log)
    {
        _sp = sp;
        _config = config;
        _log = log;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _sp.CreateScope();
        var sp = scope.ServiceProvider;

        var db = sp.GetRequiredService<OficinaDbContext>();
        _log.LogInformation("Aplicando migrations...");
        await db.Database.MigrateAsync(cancellationToken);

        var senha = _config["AdminBootstrap:Password"];
        if (string.IsNullOrWhiteSpace(senha))
        {
            _log.LogWarning("AdminBootstrap:Password não configurado — bootstrap pulado.");
            return;
        }

        var bootstrap = sp.GetRequiredService<BootstrapAdminUseCase>();
        await bootstrap.ExecutarAsync(senha, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
