using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oficina.Aplicacao.Consulta;
using Oficina.Aplicacao.Consulta.Dtos;
using Oficina.Api.Configuracao;

namespace Oficina.Api.Controllers;

/// <summary>
/// Consulta de orçamento pelo próprio cliente. Na Fase 2 era anônima, com o
/// documento vindo da query. Na Fase 3 exige token de CPF emitido pela Lambda:
/// o documento vem da claim, e uma OS de outro cliente responde 404 (não 403)
/// para não revelar a existência do número.
/// </summary>
[ApiController]
[Route("api/v1/consulta")]
[Authorize(Policy = PoliticasDeAutorizacao.RequerCliente)]
public class ConsultaController : ControllerBase
{
    [HttpGet("{numeroOs:long}")]
    public async Task<IActionResult> Consultar(
        long numeroOs,
        [FromServices] ConsultarOrdemPorNumeroUseCase uc,
        CancellationToken ct)
    {
        var documento = User.DocumentoDoCliente();
        if (documento is null) return Forbid();

        var r = await uc.ExecutarAsync(numeroOs, documento, ct);
        return MapearResultado(r);
    }

    private IActionResult MapearResultado(ResultadoConsulta r) => r switch
    {
        ResultadoConsulta.Sucesso s => Ok(s.Response),
        // Ambos os casos respondem 404 identicamente — anti-enumeracao.
        ResultadoConsulta.NaoEncontrada => NotFound(),
        ResultadoConsulta.DocumentoNaoConfere => NotFound(),
        _ => StatusCode(StatusCodes.Status500InternalServerError)
    };
}
