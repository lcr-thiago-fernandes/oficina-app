using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace Oficina.Api.Configuracao;

public static class ConfiguracaoRateLimit
{
    public const string PoliticaLogin = "login";

    public static IServiceCollection AdicionarRateLimit(this IServiceCollection services, IConfiguration configuration)
    {
        var permitLimit = configuration.GetValue<int?>("RateLimit:Login:PermitLimit") ?? 5;
        var windowMinutes = configuration.GetValue<int?>("RateLimit:Login:WindowMinutes") ?? 15;

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(PoliticaLogin, http =>
            {
                var ip = http.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                return RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = permitLimit,
                    Window = TimeSpan.FromMinutes(windowMinutes),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0
                });
            });
        });
        return services;
    }
}
