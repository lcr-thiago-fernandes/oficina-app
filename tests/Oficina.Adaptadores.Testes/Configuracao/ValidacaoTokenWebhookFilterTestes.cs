using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using Oficina.Api.Configuracao;

namespace Oficina.Adaptadores.Testes.Configuracao;

// IAuthorizationFilter roda ANTES do model binding/ModelState (e, portanto,
// antes de qualquer 400/415 gerado pela auto-validação do [ApiController]).
// Estes testes garantem que o filtro decide 401 sem nunca olhar para o corpo
// da requisição — corpo nem existe neste ponto do pipeline.
public class ValidacaoTokenWebhookFilterTestes
{
    private static AuthorizationFilterContext CriarContexto(string? tokenRecebido)
    {
        var httpContext = new DefaultHttpContext();
        if (tokenRecebido is not null)
            httpContext.Request.Headers[ValidacaoTokenWebhookFilter.NomeHeaderToken] = tokenRecebido;

        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        return new AuthorizationFilterContext(actionContext, new List<IFilterMetadata>());
    }

    private static ValidacaoTokenWebhookFilter CriarFiltro(string? tokenEsperado)
        => new(Options.Create(new WebhookOptions { Token = tokenEsperado }));

    [Fact]
    public void TokenValido_NaoDefineResultado_DeixaPipelineContinuar()
    {
        var filtro = CriarFiltro("segredo-123");
        var contexto = CriarContexto("segredo-123");

        filtro.OnAuthorization(contexto);

        contexto.Result.Should().BeNull();
    }

    [Fact]
    public void SemHeaderDeToken_DeveDefinir401_SemDependerDoCorpo()
    {
        var filtro = CriarFiltro("segredo-123");
        var contexto = CriarContexto(tokenRecebido: null);

        filtro.OnAuthorization(contexto);

        contexto.Result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public void TokenIncorreto_DeveDefinir401()
    {
        var filtro = CriarFiltro("segredo-123");
        var contexto = CriarContexto("errado");

        filtro.OnAuthorization(contexto);

        contexto.Result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public void TokenEsperadoNaoConfigurado_DeveDefinir401_FailClosed()
    {
        var filtro = CriarFiltro(tokenEsperado: null);
        var contexto = CriarContexto("qualquer");

        filtro.OnAuthorization(contexto);

        contexto.Result.Should().BeOfType<UnauthorizedResult>();
    }
}
