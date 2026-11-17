using Oficina.Infraestrutura.Mensageria.Observabilidade;

namespace Oficina.Api.Configuracao;

/// <summary>
/// Correlation id da requisição HTTP atual (definido pelo <see cref="MiddlewareDeCorrelacao"/>). É lido na hora
/// do <c>Publish</c>: com o outbox a entrega real acontece depois, sem <c>HttpContext</c>.
/// </summary>
public sealed class AcessorCorrelacaoHttp : IAcessorCorrelacao
{
    private readonly IHttpContextAccessor _http;

    public AcessorCorrelacaoHttp(IHttpContextAccessor http) => _http = http;

    public string? Atual =>
        _http.HttpContext?.Items.TryGetValue(MiddlewareDeCorrelacao.Header, out var valor) == true
            ? valor as string
            : null;
}
