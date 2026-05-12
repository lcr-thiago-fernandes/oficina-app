using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oficina.Aplicacao.Estoque;
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
        [FromServices] CriarPecaUseCase uc,
        CancellationToken ct)
    {
        var resp = await uc.ExecutarAsync(req, ct);
        return CreatedAtAction(nameof(Obter), new { id = resp.Id }, resp);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Obter(
        Guid id,
        [FromServices] ObterPecaPorIdUseCase uc,
        CancellationToken ct)
    {
        var resp = await uc.ExecutarAsync(id, ct);
        return resp is null ? NotFound() : Ok(resp);
    }

    [HttpGet]
    public async Task<IActionResult> Listar(
        [FromQuery] string? nome,
        [FromQuery] int pagina = 1,
        [FromQuery] int tamanhoPagina = 20,
        [FromQuery] bool incluirInativos = false,
        [FromServices] ListarPecasUseCase? uc = null,
        CancellationToken ct = default)
    {
        var resp = await uc!.ExecutarAsync(nome, pagina, tamanhoPagina, incluirInativos, ct);
        return Ok(resp);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Atualizar(
        Guid id,
        [FromBody] AtualizarPecaRequest req,
        [FromServices] AtualizarPecaUseCase uc,
        CancellationToken ct)
    {
        var resp = await uc.ExecutarAsync(id, req, ct);
        return resp is null ? NotFound() : Ok(resp);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Remover(
        Guid id,
        [FromServices] RemoverPecaUseCase uc,
        CancellationToken ct)
    {
        var ok = await uc.ExecutarAsync(id, ct);
        return ok ? NoContent() : NotFound();
    }

    // ===== Movimentações =====

    [HttpPost("{id:guid}/movimentacoes")]
    public async Task<IActionResult> RegistrarMovimentacao(
        Guid id,
        [FromBody] RegistrarMovimentacaoRequest req,
        [FromServices] RegistrarMovimentacaoUseCase uc,
        CancellationToken ct)
    {
        var resp = await uc.ExecutarAsync(id, req, ct);
        return resp is null ? NotFound() : Created(string.Empty, resp);
    }

    [HttpGet("{id:guid}/movimentacoes")]
    public async Task<IActionResult> ListarMovimentacoes(
        Guid id,
        [FromServices] ListarMovimentacoesUseCase uc,
        CancellationToken ct)
    {
        var resp = await uc.ExecutarAsync(id, ct);
        return resp is null ? NotFound() : Ok(resp);
    }
}
