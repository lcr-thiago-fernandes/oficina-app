using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oficina.Adaptadores.OrdensServico.Controllers;
using Oficina.Aplicacao.OrdensServico.Dtos;
using Oficina.Api.Configuracao;

namespace Oficina.Api.Controllers;

[ApiController]
[Route("api/v1/ordens-servico")]
[Authorize(Policy = PoliticasDeAutorizacao.RequerAdminOuAtendente)]
public class OrdensServicoController : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Abrir(
        [FromBody] AbrirOrdemRequest req,
        [FromServices] OrdemDeServicoController controller,
        CancellationToken ct)
    {
        var resp = await controller.AbrirAsync(req, ct);
        return CreatedAtAction(nameof(Obter), new { id = resp.Id }, resp);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Obter(
        Guid id,
        [FromServices] OrdemDeServicoController controller,
        CancellationToken ct)
    {
        var resp = await controller.ObterPorIdAsync(id, ct);
        return resp is null ? NotFound() : Ok(resp);
    }

    [HttpGet]
    public async Task<IActionResult> Listar(
        [FromServices] OrdemDeServicoController controller,
        [FromQuery] string? status = null,
        [FromQuery] int pagina = 1,
        [FromQuery] int tamanhoPagina = 20,
        CancellationToken ct = default)
    {
        var resp = await controller.ListarAsync(status, pagina, tamanhoPagina, ct);
        return Ok(resp);
    }

    // ===== Transições =====
    // Orçamento, pagamento e execução são conduzidos pela saga (Billing/Execução).

    [HttpPost("{id:guid}/entregar")]
    public async Task<IActionResult> Entregar(
        Guid id,
        [FromServices] OrdemDeServicoController controller,
        CancellationToken ct)
    {
        var resp = await controller.EntregarAsync(id, ct);
        return resp is null ? NotFound() : Ok(resp);
    }

    // ===== Métricas =====

    [HttpGet("metricas/tempo-medio")]
    [Authorize(Policy = PoliticasDeAutorizacao.RequerAdmin)]
    public async Task<IActionResult> TempoMedioExecucao(
        [FromServices] OrdemDeServicoController controller,
        CancellationToken ct)
    {
        var resp = await controller.ObterTempoMedioExecucaoAsync(ct);
        return Ok(resp);
    }
}
