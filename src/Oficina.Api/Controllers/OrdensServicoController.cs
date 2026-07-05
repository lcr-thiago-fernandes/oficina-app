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
    public async Task<IActionResult> Criar(
        [FromBody] CriarOrdemRequest req,
        [FromServices] OrdemDeServicoController controller,
        CancellationToken ct)
    {
        var resp = await controller.CriarAsync(req, ct);
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

    // ===== Itens =====

    [HttpPost("{id:guid}/servicos")]
    public async Task<IActionResult> AdicionarServico(
        Guid id,
        [FromBody] AdicionarItemServicoRequest req,
        [FromServices] OrdemDeServicoController controller,
        CancellationToken ct)
    {
        var resp = await controller.AdicionarItemServicoAsync(id, req, ct);
        return resp is null ? NotFound() : Created(string.Empty, resp);
    }

    [HttpDelete("{id:guid}/servicos/{itemId:guid}")]
    public async Task<IActionResult> RemoverServico(
        Guid id, Guid itemId,
        [FromServices] OrdemDeServicoController controller,
        CancellationToken ct)
    {
        var ok = await controller.RemoverItemServicoAsync(id, itemId, ct);
        return ok ? NoContent() : NotFound();
    }

    [HttpPost("{id:guid}/pecas")]
    public async Task<IActionResult> AdicionarPeca(
        Guid id,
        [FromBody] AdicionarItemPecaRequest req,
        [FromServices] OrdemDeServicoController controller,
        CancellationToken ct)
    {
        var resp = await controller.AdicionarItemPecaAsync(id, req, ct);
        return resp is null ? NotFound() : Created(string.Empty, resp);
    }

    [HttpDelete("{id:guid}/pecas/{itemId:guid}")]
    public async Task<IActionResult> RemoverPeca(
        Guid id, Guid itemId,
        [FromServices] OrdemDeServicoController controller,
        CancellationToken ct)
    {
        var ok = await controller.RemoverItemPecaAsync(id, itemId, ct);
        return ok ? NoContent() : NotFound();
    }

    // ===== Transições =====

    [HttpPatch("{id:guid}/diagnostico")]
    public async Task<IActionResult> IniciarDiagnostico(
        Guid id,
        [FromServices] OrdemDeServicoController controller,
        CancellationToken ct)
    {
        var resp = await controller.IniciarDiagnosticoAsync(id, ct);
        return resp is null ? NotFound() : Ok(resp);
    }

    [HttpPost("{id:guid}/orcamento/enviar")]
    public async Task<IActionResult> EnviarParaAprovacao(
        Guid id,
        [FromServices] OrdemDeServicoController controller,
        CancellationToken ct)
    {
        var resp = await controller.EnviarOrcamentoParaAprovacaoAsync(id, ct);
        return resp is null ? NotFound() : Ok(resp);
    }

    [HttpPost("{id:guid}/execucao/iniciar")]
    public async Task<IActionResult> IniciarExecucao(
        Guid id,
        [FromServices] OrdemDeServicoController controller,
        CancellationToken ct)
    {
        var resp = await controller.IniciarExecucaoAsync(id, ct);
        return resp is null ? NotFound() : Ok(resp);
    }

    [HttpPost("{id:guid}/finalizar")]
    public async Task<IActionResult> Finalizar(
        Guid id,
        [FromServices] OrdemDeServicoController controller,
        CancellationToken ct)
    {
        var resp = await controller.FinalizarAsync(id, ct);
        return resp is null ? NotFound() : Ok(resp);
    }

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
