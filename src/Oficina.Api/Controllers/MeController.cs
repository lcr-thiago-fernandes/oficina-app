using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oficina.Api.Configuracao;
using Oficina.Aplicacao.Autoatendimento;
using Oficina.Aplicacao.Autoatendimento.Dtos;
using Oficina.Aplicacao.Clientes.Dtos;

namespace Oficina.Api.Controllers;

/// <summary>
/// Autoatendimento do cliente. O documento sempre vem da claim do token
/// emitido pela Lambda — nunca de parâmetro de rota, query ou corpo.
/// </summary>
[ApiController]
[Route("api/v1/me")]
[Authorize(Policy = PoliticasDeAutorizacao.RequerCliente)]
public class MeController : ControllerBase
{
    [HttpGet("ordens-servico")]
    public async Task<IActionResult> MinhasOrdens(
        [FromServices] ListarOrdensDoClienteUseCase uc,
        CancellationToken ct)
    {
        var documento = User.DocumentoDoCliente();
        if (documento is null) return Forbid();

        var ordens = await uc.ExecutarAsync(documento, ct);
        // Cliente autenticado cujo cadastro sumiu: trata como sem conteudo,
        // nao como erro — nao ha o que ele possa fazer a respeito.
        return Ok(ordens ?? Array.Empty<OrdemResumoResponse>());
    }

    [HttpGet("veiculos")]
    public async Task<IActionResult> MeusVeiculos(
        [FromServices] ListarVeiculosDoClienteUseCase uc,
        CancellationToken ct)
    {
        var documento = User.DocumentoDoCliente();
        if (documento is null) return Forbid();

        var veiculos = await uc.ExecutarAsync(documento, ct);
        return Ok(veiculos ?? (IReadOnlyList<VeiculoResponse>)Array.Empty<VeiculoResponse>());
    }
}
