using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Oficina.Aplicacao.Clientes.Dtos;
using Oficina.Integracao.Testes.Auth;
using Xunit;

namespace Oficina.Integracao.Testes.Clientes;

[Collection(nameof(AuthCollection))]
public class ClientesEndpointTestes
{
    private readonly AuthFixture _fx;
    public ClientesEndpointTestes(AuthFixture fx) => _fx = fx;

    private async Task<HttpClient> AutenticadoAsync()
    {
        var client = _fx.Factory.CreateClient();
        var token = await _fx.ObterTokenAdminAsync();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    [Fact]
    public async Task Criar_E_Obter_DeveRetornar201E200()
    {
        var http = await AutenticadoAsync();

        var criar = await http.PostAsJsonAsync("/api/v1/clientes",
            new CriarClienteRequest("João", "39053344705", "joao@x.com", "11987654321"));

        criar.StatusCode.Should().Be(HttpStatusCode.Created);
        var criado = await criar.Content.ReadFromJsonAsync<ClienteResponse>();

        var obter = await http.GetAsync($"/api/v1/clientes/{criado!.Id}");
        obter.StatusCode.Should().Be(HttpStatusCode.OK);
        var obtido = await obter.Content.ReadFromJsonAsync<ClienteResponse>();
        obtido!.Nome.Should().Be("João");
    }

    [Fact]
    public async Task Criar_ComCpfInvalido_DeveRetornar422()
    {
        var http = await AutenticadoAsync();

        var resp = await http.PostAsJsonAsync("/api/v1/clientes",
            new CriarClienteRequest("X", "11111111111", "x@y.com", "11987654321"));

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Criar_ComDocumentoDuplicado_DeveRetornar409()
    {
        var http = await AutenticadoAsync();
        await http.PostAsJsonAsync("/api/v1/clientes",
            new CriarClienteRequest("A", "11144477735", "a@x.com", "11987654321"));

        var resp = await http.PostAsJsonAsync("/api/v1/clientes",
            new CriarClienteRequest("B", "11144477735", "b@x.com", "11987654321"));

        resp.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Criar_SemToken_DeveRetornar401()
    {
        var http = _fx.Factory.CreateClient();
        var resp = await http.PostAsJsonAsync("/api/v1/clientes",
            new CriarClienteRequest("X", "39053344705", "x@y.com", "11987654321"));
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Buscar_PorDocumento_DeveRetornarOk()
    {
        var http = await AutenticadoAsync();
        await http.PostAsJsonAsync("/api/v1/clientes",
            new CriarClienteRequest("Maria", "12345678909", "maria@x.com", "11987654321"));

        var resp = await http.GetAsync("/api/v1/clientes?documento=123.456.789-09");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var c = await resp.Content.ReadFromJsonAsync<ClienteResponse>();
        c!.Nome.Should().Be("Maria");
    }
}
