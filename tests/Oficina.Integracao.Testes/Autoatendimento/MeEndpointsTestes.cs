using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Oficina.Aplicacao.Autoatendimento.Dtos;
using Oficina.Aplicacao.Clientes.Dtos;
using Oficina.Integracao.Testes.Auth;
using Xunit;

namespace Oficina.Integracao.Testes.Autoatendimento;

[Collection(nameof(AuthCollection))]
public class MeEndpointsTestes
{
    private readonly AuthFixture _fx;
    public MeEndpointsTestes(AuthFixture fx) => _fx = fx;

    private const string Documento = "52998224725";

    private async Task<ClienteResponse> GarantirClienteAsync()
    {
        var http = _fx.Factory.CreateClient();
        http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await _fx.ObterTokenAdminAsync());

        // Checa o StatusCode explicitamente: com [ApiController], um 404 cru
        // vira ProblemDetails no corpo, que nenhum campo colide com
        // ClienteResponse — ReadFromJsonAsync sem checar sucesso desserializa
        // isso como um ClienteResponse "zerado" em vez de null, mascarando o
        // 404 e pulando a criação do cliente.
        var busca = await http.GetAsync($"/api/v1/clientes?documento={Documento}");
        if (busca.StatusCode == HttpStatusCode.OK)
            return (await busca.Content.ReadFromJsonAsync<ClienteResponse>())!;

        var criado = await http.PostAsJsonAsync("/api/v1/clientes",
            new CriarClienteRequest("Cliente Me", Documento, $"me{Guid.NewGuid():N}@x.com", "11987654321"));
        return (await criado.Content.ReadFromJsonAsync<ClienteResponse>())!;
    }

    [Fact]
    public async Task Sem_token_retorna_401()
    {
        var http = _fx.Factory.CreateClient();
        var resp = await http.GetAsync("/api/v1/me/ordens-servico");
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Token_de_Admin_retorna_403_porque_falta_o_perfil_Cliente()
    {
        var http = _fx.Factory.CreateClient();
        http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await _fx.ObterTokenAdminAsync());

        var resp = await http.GetAsync("/api/v1/me/ordens-servico");

        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Token_com_perfil_Cliente_mas_sem_claim_documento_retorna_403()
    {
        // A política RequerCliente exige DUAS coisas: perfil=Cliente e a claim
        // "documento". Este teste cobre o único ramo que não sai de graça dos
        // outros dois: perfil correto, mas sem a claim que oficina-auth-api sempre inclui.
        var http = _fx.Factory.CreateClient();
        var token = GeradorTokenDeTeste.Gerar("Cliente", Guid.NewGuid(), documento: null);
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var resp = await http.GetAsync("/api/v1/me/ordens-servico");

        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Token_de_Cliente_lista_as_proprias_ordens()
    {
        var cliente = await GarantirClienteAsync();

        var http = _fx.Factory.CreateClient();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", await _fx.ObterTokenClienteAsync(cliente.Id, Documento));

        var resp = await http.GetAsync("/api/v1/me/ordens-servico");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var ordens = await resp.Content.ReadFromJsonAsync<List<OrdemResumoResponse>>();
        ordens.Should().NotBeNull();
    }

    [Fact]
    public async Task Token_de_Cliente_lista_os_proprios_veiculos()
    {
        var cliente = await GarantirClienteAsync();

        var http = _fx.Factory.CreateClient();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", await _fx.ObterTokenClienteAsync(cliente.Id, Documento));

        var resp = await http.GetAsync("/api/v1/me/veiculos");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
