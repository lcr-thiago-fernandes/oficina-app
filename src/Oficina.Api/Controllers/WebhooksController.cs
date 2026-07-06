using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oficina.Adaptadores.OrdensServico.Controllers;
using Oficina.Aplicacao.OrdensServico.Dtos;
using Oficina.Api.Configuracao;

namespace Oficina.Api.Controllers;

// Webhook de aprovação externa de orçamento. Sem JWT ([AllowAnonymous]);
// autenticação por token de header (X-Webhook-Token), validada pelo
// ValidacaoTokenWebhookFilter (IAuthorizationFilter) ANTES do model binding,
// garantindo que 401 tem precedência sobre 400/415 quando o corpo está ausente/inválido.
[ApiController]
[AllowAnonymous]
[Route("api/v1/ordens-servico")]
public class WebhooksController : ControllerBase
{
    [HttpPost("{id:guid}/orcamento/aprovacao")]
    [ServiceFilter(typeof(ValidacaoTokenWebhookFilter))]
    public async Task<IActionResult> RegistrarDecisao(
        Guid id,
        [FromBody] DecisaoOrcamentoRequest req,
        [FromServices] OrdemDeServicoController controller,
        CancellationToken ct)
    {
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
