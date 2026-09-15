using Serilog.Context;

namespace Oficina.Api.Configuracao;

/// <summary>
/// Costura uma requisição ponta a ponta. O agente do New Relic já injeta
/// trace.id/span.id nos logs; o correlationId cobre o trecho que o APM não vê —
/// o identificador que o API Gateway e a Lambda de autenticação propagam.
/// </summary>
public class MiddlewareDeCorrelacao
{
    public const string Header = "X-Correlation-Id";

    private readonly RequestDelegate _proximo;

    public MiddlewareDeCorrelacao(RequestDelegate proximo) => _proximo = proximo;

    public async Task InvokeAsync(HttpContext contexto)
    {
        var correlationId = ObterOuGerar(contexto);

        contexto.Items[Header] = correlationId;
        contexto.Response.Headers[Header] = correlationId;

        using (LogContext.PushProperty("correlationId", correlationId))
        {
            await _proximo(contexto);
        }
    }

    private static string ObterOuGerar(HttpContext contexto)
    {
        // 1) header explicito do cliente; 2) requestId do API Gateway; 3) novo.
        if (contexto.Request.Headers.TryGetValue(Header, out var doCliente) &&
            !string.IsNullOrWhiteSpace(doCliente))
            return doCliente.ToString();

        if (contexto.Request.Headers.TryGetValue("X-Amzn-RequestId", out var doGateway) &&
            !string.IsNullOrWhiteSpace(doGateway))
            return doGateway.ToString();

        return Guid.NewGuid().ToString();
    }
}
