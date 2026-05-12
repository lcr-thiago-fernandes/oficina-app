using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Oficina.Aplicacao.Clientes.Dtos;
using Oficina.Integracao.Testes.Auth;
using Xunit;

namespace Oficina.Integracao.Testes.Clientes;

[Collection(nameof(AuthCollection))]
public class VeiculosEndpointTestes
{
    private readonly AuthFixture _fx;
    public VeiculosEndpointTestes(AuthFixture fx) => _fx = fx;

    private async Task<(HttpClient http, Guid clienteId)> ContextoAsync()
    {
        var http = _fx.Factory.CreateClient();
        var token = await _fx.ObterTokenAdminAsync();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var criar = await http.PostAsJsonAsync("/api/v1/clientes",
            new CriarClienteRequest("Test", "39053344705", $"v{Guid.NewGuid():N}@x.com", "11987654321"));
        // se o CPF acima já estiver em uso, criar gera 409 — para fins do teste, ignoramos e buscamos por documento:
        var resp = await http.GetAsync("/api/v1/clientes?documento=39053344705");
        var c = await resp.Content.ReadFromJsonAsync<ClienteResponse>();
        return (http, c!.Id);
    }

    [Fact]
    public async Task Adicionar_Listar_Atualizar_Remover_Veiculo_FluxoCompleto()
    {
        var (http, clienteId) = await ContextoAsync();

        // Adicionar
        var add = await http.PostAsJsonAsync($"/api/v1/clientes/{clienteId}/veiculos",
            new AdicionarVeiculoRequest("XYZ9988", "Fiat", "Uno", 2020));
        add.StatusCode.Should().Be(HttpStatusCode.Created);

        // Listar
        var listar = await http.GetAsync($"/api/v1/clientes/{clienteId}/veiculos");
        listar.StatusCode.Should().Be(HttpStatusCode.OK);
        var lista = await listar.Content.ReadFromJsonAsync<List<VeiculoResponse>>();
        lista!.Should().Contain(v => v.Placa == "XYZ9988");

        // Atualizar
        var upd = await http.PutAsJsonAsync($"/api/v1/clientes/{clienteId}/veiculos/XYZ9988",
            new AtualizarVeiculoRequest("VW", "Gol", 2018));
        upd.StatusCode.Should().Be(HttpStatusCode.OK);

        // Remover
        var del = await http.DeleteAsync($"/api/v1/clientes/{clienteId}/veiculos/XYZ9988");
        del.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Adicionar_ComPlacaInvalida_DeveRetornar422()
    {
        var (http, clienteId) = await ContextoAsync();

        var resp = await http.PostAsJsonAsync($"/api/v1/clientes/{clienteId}/veiculos",
            new AdicionarVeiculoRequest("INVALID", "F", "U", 2020));

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }
}
