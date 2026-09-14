using System.Security.Claims;
using FluentAssertions;
using Oficina.Api.Configuracao;
using Xunit;

namespace Oficina.Api.Testes.Configuracao;

public class ExtensoesClaimsTestes
{
    private static ClaimsPrincipal Principal(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, "Test"));

    [Fact]
    public void DocumentoDoCliente_retorna_o_valor_da_claim()
    {
        var p = Principal(new Claim("documento", "11144477735"));
        p.DocumentoDoCliente().Should().Be("11144477735");
    }

    [Fact]
    public void DocumentoDoCliente_retorna_nulo_quando_a_claim_esta_ausente()
    {
        Principal().DocumentoDoCliente().Should().BeNull();
    }

}
