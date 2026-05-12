using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oficina.Aplicacao.Clientes;
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
        [FromServices] CriarClienteUseCase uc,
        CancellationToken ct)
    {
        var resp = await uc.ExecutarAsync(req, ct);
        return CreatedAtAction(nameof(Obter), new { id = resp.Id }, resp);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Obter(
        Guid id,
        [FromServices] ObterClientePorIdUseCase uc,
        CancellationToken ct)
    {
        var resp = await uc.ExecutarAsync(id, ct);
        return resp is null ? NotFound() : Ok(resp);
    }

    [HttpGet]
    public async Task<IActionResult> Buscar(
        [FromServices] BuscarClientePorDocumentoUseCase buscar,
        [FromServices] ListarClientesUseCase listar,
        [FromQuery] string? documento = null,
        [FromQuery] int pagina = 1,
        [FromQuery] int tamanhoPagina = 20,
        CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(documento))
        {
            var c = await buscar.ExecutarAsync(documento, ct);
            return c is null ? NotFound() : Ok(c);
        }
        var resultado = await listar.ExecutarAsync(pagina, tamanhoPagina, ct);
        return Ok(resultado);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Atualizar(
        Guid id,
        [FromBody] AtualizarClienteRequest req,
        [FromServices] AtualizarClienteUseCase uc,
        CancellationToken ct)
    {
        var resp = await uc.ExecutarAsync(id, req, ct);
        return resp is null ? NotFound() : Ok(resp);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Remover(
        Guid id,
        [FromServices] RemoverClienteUseCase uc,
        CancellationToken ct)
    {
        var ok = await uc.ExecutarAsync(id, ct);
        return ok ? NoContent() : NotFound();
    }

    // ===== Veículos =====

    [HttpGet("{id:guid}/veiculos")]
    public async Task<IActionResult> ListarVeiculos(
        Guid id,
        [FromServices] ListarVeiculosUseCase uc,
        CancellationToken ct)
    {
        var lista = await uc.ExecutarAsync(id, ct);
        return lista is null ? NotFound() : Ok(lista);
    }

    [HttpPost("{id:guid}/veiculos")]
    public async Task<IActionResult> AdicionarVeiculo(
        Guid id,
        [FromBody] AdicionarVeiculoRequest req,
        [FromServices] AdicionarVeiculoUseCase uc,
        CancellationToken ct)
    {
        var resp = await uc.ExecutarAsync(id, req, ct);
        return resp is null
            ? NotFound()
            : CreatedAtAction(nameof(ListarVeiculos), new { id }, resp);
    }

    [HttpPut("{id:guid}/veiculos/{placa}")]
    public async Task<IActionResult> AtualizarVeiculo(
        Guid id,
        string placa,
        [FromBody] AtualizarVeiculoRequest req,
        [FromServices] AtualizarVeiculoUseCase uc,
        CancellationToken ct)
    {
        var resp = await uc.ExecutarAsync(id, placa, req, ct);
        return resp is null ? NotFound() : Ok(resp);
    }

    [HttpDelete("{id:guid}/veiculos/{placa}")]
    public async Task<IActionResult> RemoverVeiculo(
        Guid id,
        string placa,
        [FromServices] RemoverVeiculoUseCase uc,
        CancellationToken ct)
    {
        var ok = await uc.ExecutarAsync(id, placa, ct);
        return ok ? NoContent() : NotFound();
    }
}
