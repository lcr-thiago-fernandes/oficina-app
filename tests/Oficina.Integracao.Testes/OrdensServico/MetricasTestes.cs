using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Oficina.Aplicacao.OrdensServico.Dtos;
using Oficina.Integracao.Testes.Auth;
using Xunit;

namespace Oficina.Integracao.Testes.OrdensServico;

[Collection(nameof(AuthCollection))]
public class MetricasTestes
{
    private readonly AuthFixture _fx;
    public MetricasTestes(AuthFixture fx) => _fx = fx;

    [Fact]
    public async Task TempoMedio_ComoAdmin_DeveRetornar200()
    {
        var http = _fx.Factory.CreateClient();
        var token = await _fx.ObterTokenAdminAsync();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var resp = await http.GetAsync("/api/v1/ordens-servico/metricas/tempo-medio");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await resp.Content.ReadFromJsonAsync<MetricasTempoMedioResponse>();
        body.Should().NotBeNull();
        body!.TotalOrdensConcluidas.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task TempoMedio_SemToken_DeveRetornar401()
    {
        var http = _fx.Factory.CreateClient();
        var resp = await http.GetAsync("/api/v1/ordens-servico/metricas/tempo-medio");
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
