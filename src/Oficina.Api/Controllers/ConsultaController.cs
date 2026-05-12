using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oficina.Aplicacao.Consulta;
using Oficina.Aplicacao.Consulta.Dtos;

namespace Oficina.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/v1/consulta")]
public class ConsultaController : ControllerBase
{
    [HttpGet("{numeroOs:long}")]
    public async Task<IActionResult> Consultar(
        long numeroOs,
        [FromQuery] string documento,
        [FromServices] ConsultarOrdemPorNumeroUseCase uc,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(documento))
            return BadRequest(new { erro = "Parâmetro 'documento' é obrigatório." });

        var r = await uc.ExecutarAsync(numeroOs, documento, ct);
        return MapearResultado(r);
    }

    [HttpPost("{numeroOs:long}/aprovar")]
    public async Task<IActionResult> Aprovar(
        long numeroOs,
        [FromQuery] string documento,
        [FromServices] AprovarOrcamentoPorClienteUseCase uc,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(documento))
            return BadRequest(new { erro = "Parâmetro 'documento' é obrigatório." });

        var r = await uc.ExecutarAsync(numeroOs, documento, ct);
        return MapearResultado(r);
    }

    [HttpPost("{numeroOs:long}/rejeitar")]
    public async Task<IActionResult> Rejeitar(
        long numeroOs,
        [FromQuery] string documento,
        [FromServices] RejeitarOrcamentoPorClienteUseCase uc,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(documento))
            return BadRequest(new { erro = "Parâmetro 'documento' é obrigatório." });

        var r = await uc.ExecutarAsync(numeroOs, documento, ct);
        return MapearResultado(r);
    }

    private IActionResult MapearResultado(ResultadoConsulta r) => r switch
    {
        ResultadoConsulta.Sucesso s => Ok(s.Response),
        // Para evitar enumeração, ambos os casos retornam 404 com a mesma resposta
        ResultadoConsulta.NaoEncontrada => NotFound(),
        ResultadoConsulta.DocumentoNaoConfere => NotFound(),
        _ => StatusCode(StatusCodes.Status500InternalServerError)
    };
}
