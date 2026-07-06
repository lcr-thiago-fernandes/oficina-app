using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;
using Oficina.Adaptadores.OrdensServico.Webhooks;

namespace Oficina.Api.Configuracao;

// Filtro de autorização (roda ANTES do model binding/ModelState) que valida o
// header X-Webhook-Token. Garante que 401 tem precedência sobre 400/415 quando
// a requisição chega sem token e sem corpo (ou corpo inválido).
public class ValidacaoTokenWebhookFilter : IAuthorizationFilter
{
    public const string NomeHeaderToken = "X-Webhook-Token";

    private readonly WebhookOptions _options;

    public ValidacaoTokenWebhookFilter(IOptions<WebhookOptions> options)
    {
        _options = options.Value;
    }

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var recebido = context.HttpContext.Request.Headers[NomeHeaderToken].ToString();

        if (!ValidadorTokenWebhook.EhTokenValido(recebido, _options.Token))
            context.Result = new UnauthorizedResult();
    }
}
