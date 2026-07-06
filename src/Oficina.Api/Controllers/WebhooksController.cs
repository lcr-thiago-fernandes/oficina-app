using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Oficina.Adaptadores.OrdensServico.Controllers;
using Oficina.Adaptadores.OrdensServico.Webhooks;
using Oficina.Aplicacao.OrdensServico.Dtos;
using Oficina.Api.Configuracao;

namespace Oficina.Api.Controllers;

// Webhook de aprovação externa de orçamento. Sem JWT ([AllowAnonymous]);
// autenticação por token de header (X-Webhook-Token) validado contra Webhook:Token.
[ApiController]
[AllowAnonymous]
[Route("api/v1/ordens-servico")]
public class WebhooksController : ControllerBase
{
    public const string NomeHeaderToken = "X-Webhook-Token";

    [HttpPost("{id:guid}/orcamento/aprovacao")]
    public async Task<IActionResult> RegistrarDecisao(
        Guid id,
        [FromBody] DecisaoOrcamentoRequest req,
        [FromHeader(Name = NomeHeaderToken)] string? token,
        [FromServices] OrdemDeServicoController controller,
        [FromServices] IOptions<WebhookOptions> webhook,
        CancellationToken ct)
    {
        if (!ValidadorTokenWebhook.EhTokenValido(token, webhook.Value.Token))
            return Unauthorized();

        if (!TentarInterpretarDecisao(req.Decisao, out var aprovado))
            return UnprocessableEntity(new ProblemDetails
            {
                Title = "Decisão inválida",
                Detail = "Use 'aprovado' ou 'recusado'.",
                Status = StatusCodes.Status422UnprocessableEntity
            });

        var resp = await controller.RegistrarDecisaoDeOrcamentoAsync(id, aprovado, ct);
        return resp is null ? NotFound() : Ok(resp);
    }

    private static bool TentarInterpretarDecisao(string? decisao, out bool aprovado)
    {
        aprovado = false;
        switch (decisao?.Trim().ToLowerInvariant())
        {
            case "aprovado": aprovado = true; return true;
            case "recusado": aprovado = false; return true;
            default: return false;
        }
    }
}
