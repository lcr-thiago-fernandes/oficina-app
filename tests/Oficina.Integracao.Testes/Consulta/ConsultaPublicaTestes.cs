using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Oficina.Aplicacao.Catalogo.Dtos;
using Oficina.Aplicacao.Clientes.Dtos;
using Oficina.Aplicacao.Consulta.Dtos;
using Oficina.Aplicacao.OrdensServico.Dtos;
using Oficina.Integracao.Testes.Auth;
using Xunit;

namespace Oficina.Integracao.Testes.Consulta;

[Collection(nameof(AuthCollection))]
public class ConsultaPublicaTestes
{
    private readonly AuthFixture _fx;
    public ConsultaPublicaTestes(AuthFixture fx) => _fx = fx;

    private async Task<HttpClient> AdminAsync()
    {
        var http = _fx.Factory.CreateClient();
        var token = await _fx.ObterTokenAdminAsync();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return http;
    }

    private async Task<(long numero, string documento)> CriarOsParaConsultaAsync()
    {
        var http = await AdminAsync();
        var doc = "11144477735";

        var cli = await (await http.GetAsync($"/api/v1/clientes?documento={doc}"))
            .Content.ReadFromJsonAsync<ClienteResponse>();
        if (cli is null)
        {
            var r = await http.PostAsJsonAsync("/api/v1/clientes",
                new CriarClienteRequest("Cli OS", doc, $"c{Guid.NewGuid():N}@x.com", "11987654321"));
            cli = await r.Content.ReadFromJsonAsync<ClienteResponse>();
        }
        var v = await (await http.PostAsJsonAsync($"/api/v1/clientes/{cli!.Id}/veiculos",
            new AdicionarVeiculoRequest($"OSC{new Random().Next(1000,9999)}", "F", "U", 2020)))
            .Content.ReadFromJsonAsync<VeiculoResponse>();

        var os = await (await http.PostAsJsonAsync("/api/v1/ordens-servico",
            new CriarOrdemRequest(cli.Id, v!.Id, null))).Content.ReadFromJsonAsync<OrdemResponse>();
        await http.PatchAsync($"/api/v1/ordens-servico/{os!.Id}/diagnostico", null);

        // adicionar pelo menos um item para poder enviar para aprovação
        var s = await (await http.PostAsJsonAsync("/api/v1/servicos",
            new CriarServicoRequest("S consulta", "x", 10m, 10)))
            .Content.ReadFromJsonAsync<ServicoResponse>();
        await http.PostAsJsonAsync($"/api/v1/ordens-servico/{os.Id}/servicos",
            new AdicionarItemServicoRequest(s!.Id, 1));
        await http.PostAsync($"/api/v1/ordens-servico/{os.Id}/orcamento/enviar", null);

        return (os.Numero, doc);
    }

    [Fact]
    public async Task Consultar_ComDocumentoCorreto_DeveRetornar200()
    {
        var (numero, doc) = await CriarOsParaConsultaAsync();
        var publico = _fx.Factory.CreateClient();

        var resp = await publico.GetAsync($"/api/v1/consulta/{numero}?documento={doc}");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadFromJsonAsync<ConsultaPublicaResponse>();
        body!.Numero.Should().Be(numero);
        body.Status.Should().Be("AguardandoAprovacao");
        body.DocumentoMascarado.Should().NotContain("***");
    }

    [Fact]
    public async Task Consultar_ComDocumentoErrado_DeveRetornar404()
    {
        var (numero, _) = await CriarOsParaConsultaAsync();
        var publico = _fx.Factory.CreateClient();

        var resp = await publico.GetAsync($"/api/v1/consulta/{numero}?documento=39053344705");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Consultar_OsInexistente_DeveRetornar404()
    {
        var publico = _fx.Factory.CreateClient();
        var resp = await publico.GetAsync("/api/v1/consulta/9999999?documento=11144477735");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Consultar_SemDocumento_DeveRetornar400()
    {
        var publico = _fx.Factory.CreateClient();
        var resp = await publico.GetAsync("/api/v1/consulta/1");
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Aprovar_PeloCliente_DeveTransitar()
    {
        var (numero, doc) = await CriarOsParaConsultaAsync();
        var publico = _fx.Factory.CreateClient();

        var resp = await publico.PostAsync($"/api/v1/consulta/{numero}/aprovar?documento={doc}", null);

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadFromJsonAsync<ConsultaPublicaResponse>();
        body!.OrcamentoAprovadoEm.Should().NotBeNull();
    }

    [Fact]
    public async Task Rejeitar_PeloCliente_DeveCancelar()
    {
        var (numero, doc) = await CriarOsParaConsultaAsync();
        var publico = _fx.Factory.CreateClient();

        var resp = await publico.PostAsync($"/api/v1/consulta/{numero}/rejeitar?documento={doc}", null);

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadFromJsonAsync<ConsultaPublicaResponse>();
        body!.Status.Should().Be("Cancelada");
    }
}
