using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace Oficina.Api.Configuracao;

public static class ConfiguracaoRateLimit
{
    public const string PoliticaLogin = "login";

    public static IServiceCollection AdicionarRateLimit(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(PoliticaLogin, http =>
            {
                var ip = http.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                return RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 5,
                    Window = TimeSpan.FromMinutes(15),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0
                });
            });
        });
        return services;
    }
}
