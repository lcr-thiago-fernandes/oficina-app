using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oficina.Aplicacao.Catalogo;
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
        [FromServices] CriarServicoUseCase uc,
        CancellationToken ct)
    {
        var resp = await uc.ExecutarAsync(req, ct);
        return CreatedAtAction(nameof(Obter), new { id = resp.Id }, resp);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Obter(
        Guid id,
        [FromServices] ObterServicoPorIdUseCase uc,
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
        [FromServices] ListarServicosUseCase? uc = null,
        CancellationToken ct = default)
    {
        var resp = await uc!.ExecutarAsync(nome, pagina, tamanhoPagina, incluirInativos, ct);
        return Ok(resp);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Atualizar(
        Guid id,
        [FromBody] AtualizarServicoRequest req,
        [FromServices] AtualizarServicoUseCase uc,
        CancellationToken ct)
    {
        var resp = await uc.ExecutarAsync(id, req, ct);
        return resp is null ? NotFound() : Ok(resp);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Remover(
        Guid id,
        [FromServices] RemoverServicoUseCase uc,
        CancellationToken ct)
    {
        var ok = await uc.ExecutarAsync(id, ct);
        return ok ? NoContent() : NotFound();
    }
}
