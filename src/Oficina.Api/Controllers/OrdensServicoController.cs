using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oficina.Aplicacao.OrdensServico;
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
        [FromServices] CriarOrdemUseCase uc,
        CancellationToken ct)
    {
        var resp = await uc.ExecutarAsync(req, ct);
        return CreatedAtAction(nameof(Obter), new { id = resp.Id }, resp);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Obter(
        Guid id,
        [FromServices] ObterOrdemPorIdUseCase uc,
        CancellationToken ct)
    {
        var resp = await uc.ExecutarAsync(id, ct);
        return resp is null ? NotFound() : Ok(resp);
    }

    [HttpGet]
    public async Task<IActionResult> Listar(
        [FromQuery] string? status,
        [FromQuery] int pagina = 1,
        [FromQuery] int tamanhoPagina = 20,
        [FromServices] ListarOrdensUseCase? uc = null,
        CancellationToken ct = default)
    {
        var resp = await uc!.ExecutarAsync(status, pagina, tamanhoPagina, ct);
        return Ok(resp);
    }

    // ===== Itens =====

    [HttpPost("{id:guid}/servicos")]
    public async Task<IActionResult> AdicionarServico(
        Guid id,
        [FromBody] AdicionarItemServicoRequest req,
        [FromServices] AdicionarItemServicoUseCase uc,
        CancellationToken ct)
    {
        var resp = await uc.ExecutarAsync(id, req, ct);
        return resp is null ? NotFound() : Created(string.Empty, resp);
    }

    [HttpDelete("{id:guid}/servicos/{itemId:guid}")]
    public async Task<IActionResult> RemoverServico(
        Guid id, Guid itemId,
        [FromServices] RemoverItemServicoUseCase uc,
        CancellationToken ct)
    {
        var ok = await uc.ExecutarAsync(id, itemId, ct);
        return ok ? NoContent() : NotFound();
    }

    [HttpPost("{id:guid}/pecas")]
    public async Task<IActionResult> AdicionarPeca(
        Guid id,
        [FromBody] AdicionarItemPecaRequest req,
        [FromServices] AdicionarItemPecaUseCase uc,
        CancellationToken ct)
    {
        var resp = await uc.ExecutarAsync(id, req, ct);
        return resp is null ? NotFound() : Created(string.Empty, resp);
    }

    [HttpDelete("{id:guid}/pecas/{itemId:guid}")]
    public async Task<IActionResult> RemoverPeca(
        Guid id, Guid itemId,
        [FromServices] RemoverItemPecaUseCase uc,
        CancellationToken ct)
    {
        var ok = await uc.ExecutarAsync(id, itemId, ct);
        return ok ? NoContent() : NotFound();
    }

    // ===== Transições =====

    [HttpPatch("{id:guid}/diagnostico")]
    public async Task<IActionResult> IniciarDiagnostico(
        Guid id,
        [FromServices] IniciarDiagnosticoUseCase uc,
        CancellationToken ct)
    {
        var resp = await uc.ExecutarAsync(id, ct);
        return resp is null ? NotFound() : Ok(resp);
    }

    [HttpPost("{id:guid}/orcamento/enviar")]
    public async Task<IActionResult> EnviarParaAprovacao(
        Guid id,
        [FromServices] EnviarOrcamentoParaAprovacaoUseCase uc,
        CancellationToken ct)
    {
        var resp = await uc.ExecutarAsync(id, ct);
        return resp is null ? NotFound() : Ok(resp);
    }

    [HttpPost("{id:guid}/execucao/iniciar")]
    public async Task<IActionResult> IniciarExecucao(
        Guid id,
        [FromServices] IniciarExecucaoUseCase uc,
        CancellationToken ct)
    {
        var resp = await uc.ExecutarAsync(id, ct);
        return resp is null ? NotFound() : Ok(resp);
    }

    [HttpPost("{id:guid}/finalizar")]
    public async Task<IActionResult> Finalizar(
        Guid id,
        [FromServices] FinalizarOrdemUseCase uc,
        CancellationToken ct)
    {
        var resp = await uc.ExecutarAsync(id, ct);
        return resp is null ? NotFound() : Ok(resp);
    }

    [HttpPost("{id:guid}/entregar")]
    public async Task<IActionResult> Entregar(
        Guid id,
        [FromServices] EntregarOrdemUseCase uc,
        CancellationToken ct)
    {
        var resp = await uc.ExecutarAsync(id, ct);
        return resp is null ? NotFound() : Ok(resp);
    }

    // ===== Métricas =====

    [HttpGet("metricas/tempo-medio")]
    [Authorize(Policy = PoliticasDeAutorizacao.RequerAdmin)]
    public async Task<IActionResult> TempoMedioExecucao(
        [FromServices] ObterTempoMedioExecucaoUseCase uc,
        CancellationToken ct)
    {
        var resp = await uc.ExecutarAsync(ct);
        return Ok(resp);
    }
}
