using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oficina.Adaptadores.Catalogo.Controllers;
using Oficina.Aplicacao.Catalogo.Dtos;
using Oficina.Api.Configuracao;

namespace Oficina.Api.Controllers;

[ApiController]
[Route("api/v1/servicos")]
[Authorize(Policy = PoliticasDeAutorizacao.RequerAdminOuAtendente)]
public class ServicosController : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Criar(
        [FromBody] CriarServicoRequest req,
        [FromServices] ServicoController controller,
        CancellationToken ct)
    {
        var resp = await controller.CriarAsync(req, ct);
        return CreatedAtAction(nameof(Obter), new { id = resp.Id }, resp);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Obter(
        Guid id,
        [FromServices] ServicoController controller,
        CancellationToken ct)
    {
        var resp = await controller.ObterPorIdAsync(id, ct);
        return resp is null ? NotFound() : Ok(resp);
    }

    [HttpGet]
    public async Task<IActionResult> Listar(
        [FromServices] ServicoController controller,
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
        [FromBody] AtualizarServicoRequest req,
        [FromServices] ServicoController controller,
        CancellationToken ct)
    {
        var resp = await controller.AtualizarAsync(id, req, ct);
        return resp is null ? NotFound() : Ok(resp);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Remover(
        Guid id,
        [FromServices] ServicoController controller,
        CancellationToken ct)
    {
        var ok = await controller.RemoverAsync(id, ct);
        return ok ? NoContent() : NotFound();
    }
}
