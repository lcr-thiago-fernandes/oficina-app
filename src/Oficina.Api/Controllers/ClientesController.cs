using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oficina.Adaptadores.Clientes.Controllers;
using Oficina.Aplicacao.Clientes.Dtos;
using Oficina.Api.Configuracao;

namespace Oficina.Api.Controllers;

[ApiController]
[Route("api/v1/clientes")]
[Authorize(Policy = PoliticasDeAutorizacao.RequerAdminOuAtendente)]
public class ClientesController : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Criar(
        [FromBody] CriarClienteRequest req,
        [FromServices] ClienteController controller,
        CancellationToken ct)
    {
        var resp = await controller.CriarAsync(req, ct);
        return CreatedAtAction(nameof(Obter), new { id = resp.Id }, resp);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Obter(
        Guid id,
        [FromServices] ClienteController controller,
        CancellationToken ct)
    {
        var resp = await controller.ObterPorIdAsync(id, ct);
        return resp is null ? NotFound() : Ok(resp);
    }

    [HttpGet]
    public async Task<IActionResult> Buscar(
        [FromServices] ClienteController controller,
        [FromQuery] string? documento = null,
        [FromQuery] int pagina = 1,
        [FromQuery] int tamanhoPagina = 20,
        CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(documento))
        {
            var c = await controller.BuscarPorDocumentoAsync(documento, ct);
            return c is null ? NotFound() : Ok(c);
        }
        var resultado = await controller.ListarAsync(pagina, tamanhoPagina, ct);
        return Ok(resultado);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Atualizar(
        Guid id,
        [FromBody] AtualizarClienteRequest req,
        [FromServices] ClienteController controller,
        CancellationToken ct)
    {
        var resp = await controller.AtualizarAsync(id, req, ct);
        return resp is null ? NotFound() : Ok(resp);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Remover(
        Guid id,
        [FromServices] ClienteController controller,
        CancellationToken ct)
    {
        var ok = await controller.RemoverAsync(id, ct);
        return ok ? NoContent() : NotFound();
    }

    // ===== Veículos =====

    [HttpGet("{id:guid}/veiculos")]
    public async Task<IActionResult> ListarVeiculos(
        Guid id,
        [FromServices] ClienteController controller,
        CancellationToken ct)
    {
        var lista = await controller.ListarVeiculosAsync(id, ct);
        return lista is null ? NotFound() : Ok(lista);
    }

    [HttpPost("{id:guid}/veiculos")]
    public async Task<IActionResult> AdicionarVeiculo(
        Guid id,
        [FromBody] AdicionarVeiculoRequest req,
        [FromServices] ClienteController controller,
        CancellationToken ct)
    {
        var resp = await controller.AdicionarVeiculoAsync(id, req, ct);
        return resp is null
            ? NotFound()
            : CreatedAtAction(nameof(ListarVeiculos), new { id }, resp);
    }

    [HttpPut("{id:guid}/veiculos/{placa}")]
    public async Task<IActionResult> AtualizarVeiculo(
        Guid id,
        string placa,
        [FromBody] AtualizarVeiculoRequest req,
        [FromServices] ClienteController controller,
        CancellationToken ct)
    {
        var resp = await controller.AtualizarVeiculoAsync(id, placa, req, ct);
        return resp is null ? NotFound() : Ok(resp);
    }

    [HttpDelete("{id:guid}/veiculos/{placa}")]
    public async Task<IActionResult> RemoverVeiculo(
        Guid id,
        string placa,
        [FromServices] ClienteController controller,
        CancellationToken ct)
    {
        var ok = await controller.RemoverVeiculoAsync(id, placa, ct);
        return ok ? NoContent() : NotFound();
    }
}
