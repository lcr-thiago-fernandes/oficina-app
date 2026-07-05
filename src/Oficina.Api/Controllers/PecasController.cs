using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oficina.Adaptadores.Estoque.Controllers;
using Oficina.Aplicacao.Estoque.Dtos;
using Oficina.Api.Configuracao;

namespace Oficina.Api.Controllers;

[ApiController]
[Route("api/v1/pecas")]
[Authorize(Policy = PoliticasDeAutorizacao.RequerAdminOuAtendente)]
public class PecasController : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Criar(
        [FromBody] CriarPecaRequest req,
        [FromServices] PecaController controller,
        CancellationToken ct)
    {
        var resp = await controller.CriarAsync(req, ct);
        return CreatedAtAction(nameof(Obter), new { id = resp.Id }, resp);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Obter(
        Guid id,
        [FromServices] PecaController controller,
        CancellationToken ct)
    {
        var resp = await controller.ObterPorIdAsync(id, ct);
        return resp is null ? NotFound() : Ok(resp);
    }

    [HttpGet]
    public async Task<IActionResult> Listar(
        [FromServices] PecaController controller,
        [FromQuery] string? nome,
        [FromQuery] int pagina = 1,
        [FromQuery] int tamanhoPagina = 20,
        [FromQuery] bool incluirInativos = false,
        CancellationToken ct = default)
    {
        var resp = await controller.ListarAsync(nome, pagina, tamanhoPagina, incluirInativos, ct);
        return Ok(resp);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Atualizar(
        Guid id,
        [FromBody] AtualizarPecaRequest req,
        [FromServices] PecaController controller,
        CancellationToken ct)
    {
        var resp = await controller.AtualizarAsync(id, req, ct);
        return resp is null ? NotFound() : Ok(resp);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Remover(
        Guid id,
        [FromServices] PecaController controller,
        CancellationToken ct)
    {
        var ok = await controller.RemoverAsync(id, ct);
        return ok ? NoContent() : NotFound();
    }

    // ===== Movimentações =====

    [HttpPost("{id:guid}/movimentacoes")]
    public async Task<IActionResult> RegistrarMovimentacao(
        Guid id,
        [FromBody] RegistrarMovimentacaoRequest req,
        [FromServices] PecaController controller,
        CancellationToken ct)
    {
        var resp = await controller.RegistrarMovimentacaoAsync(id, req, ct);
        return resp is null ? NotFound() : Created(string.Empty, resp);
    }

    [HttpGet("{id:guid}/movimentacoes")]
    public async Task<IActionResult> ListarMovimentacoes(
        Guid id,
        [FromServices] PecaController controller,
        CancellationToken ct)
    {
        var resp = await controller.ListarMovimentacoesAsync(id, ct);
        return resp is null ? NotFound() : Ok(resp);
    }
}
