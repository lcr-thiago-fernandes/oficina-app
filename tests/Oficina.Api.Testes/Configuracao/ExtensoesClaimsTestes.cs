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

    [Fact]
    public void IdDoSujeito_converte_a_claim_sub_em_Guid()
    {
        var id = Guid.NewGuid();
        var p = Principal(new Claim("sub", id.ToString()));
        p.IdDoSujeito().Should().Be(id);
    }

    [Fact]
    public void IdDoSujeito_retorna_nulo_quando_sub_nao_e_um_Guid()
    {
        var p = Principal(new Claim("sub", "nao-e-guid"));
        p.IdDoSujeito().Should().BeNull();
    }
}
