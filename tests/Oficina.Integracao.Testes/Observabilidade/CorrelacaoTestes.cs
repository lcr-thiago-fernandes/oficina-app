using FluentAssertions;
using Oficina.Integracao.Testes.Auth;
using Xunit;

namespace Oficina.Integracao.Testes.Observabilidade;

[Collection(nameof(AuthCollection))]
public class CorrelacaoTestes
{
    private readonly AuthFixture _fx;
    public CorrelacaoTestes(AuthFixture fx) => _fx = fx;

    [Fact]
    public async Task Resposta_devolve_o_correlation_id_enviado_pelo_cliente()
    {
        var http = _fx.Factory.CreateClient();
        var enviado = Guid.NewGuid().ToString();
        http.DefaultRequestHeaders.Add("X-Correlation-Id", enviado);

        var resp = await http.GetAsync("/health");

        resp.Headers.GetValues("X-Correlation-Id").Single().Should().Be(enviado);
    }

    [Fact]
    public async Task Resposta_gera_um_correlation_id_quando_o_cliente_nao_envia()
    {
        var http = _fx.Factory.CreateClient();

        var resp = await http.GetAsync("/health");

        resp.Headers.TryGetValues("X-Correlation-Id", out var valores).Should().BeTrue();
        Guid.TryParse(valores!.Single(), out _).Should().BeTrue();
    }
}
