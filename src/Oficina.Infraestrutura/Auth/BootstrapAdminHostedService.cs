using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Oficina.Infraestrutura.Persistencia;

namespace Oficina.Infraestrutura.Auth;

/// <summary>
/// No startup do host (compose/local/testes) aplica migrations + bootstrap do admin.
/// Em Kubernetes o ConfigMap define Bootstrap:ExecutarNoStartup=false, então os pods
/// do Deployment NÃO migram — quem migra é o Job dedicado (modo "migrate" do Program).
/// </summary>
public class BootstrapAdminHostedService : IHostedService
{
    private readonly IServiceProvider _sp;
    private readonly IConfiguration _config;
    private readonly ILogger<BootstrapAdminHostedService> _log;
    private readonly IInicializadorBanco _inicializador;

    public BootstrapAdminHostedService(
        IServiceProvider sp,
        IConfiguration config,
        ILogger<BootstrapAdminHostedService> log,
        IInicializadorBanco inicializador)
    {
        _sp = sp;
        _config = config;
        _log = log;
        _inicializador = inicializador;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!InicializadorBanco.DeveExecutarNoStartup(_config))
        {
            _log.LogInformation(
                "Bootstrap:ExecutarNoStartup=false — inicialização no startup pulada " +
                "(modo Kubernetes; a migração é feita pelo Job dedicado).");
            return;
        }

        await _inicializador.ExecutarAsync(_sp, _config, _log, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
