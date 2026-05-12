using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Oficina.Aplicacao.Catalogo;
using Oficina.Aplicacao.Catalogo.Dtos;
using Oficina.Integracao.Testes.Auth;
using Xunit;

namespace Oficina.Integracao.Testes.Catalogo;

[Collection(nameof(AuthCollection))]
public class ServicosEndpointTestes
{
    private readonly AuthFixture _fx;
    public ServicosEndpointTestes(AuthFixture fx) => _fx = fx;

    private async Task<HttpClient> AutenticadoAsync()
    {
        var http = _fx.Factory.CreateClient();
        var token = await _fx.ObterTokenAdminAsync();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return http;
    }

    [Fact]
    public async Task FluxoCompleto_CriarObterAtualizarRemover()
    {
        var http = await AutenticadoAsync();

        // Criar
        var criar = await http.PostAsJsonAsync("/api/v1/servicos",
            new CriarServicoRequest("Alinhamento", "Alinhamento e balanceamento", 90m, 60));
        criar.StatusCode.Should().Be(HttpStatusCode.Created);
        var criado = await criar.Content.ReadFromJsonAsync<ServicoResponse>();

        // Obter
        var obter = await http.GetAsync($"/api/v1/servicos/{criado!.Id}");
        obter.StatusCode.Should().Be(HttpStatusCode.OK);

        // Atualizar
        var upd = await http.PutAsJsonAsync($"/api/v1/servicos/{criado.Id}",
            new AtualizarServicoRequest("Alinhamento Premium", "x", 120m, 75));
        upd.StatusCode.Should().Be(HttpStatusCode.OK);
        var atualizado = await upd.Content.ReadFromJsonAsync<ServicoResponse>();
        atualizado!.PrecoBase.Should().Be(120m);

        // Remover
        var del = await http.DeleteAsync($"/api/v1/servicos/{criado.Id}");
        del.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Conferir que sumiu da listagem padrão
        var listar = await http.GetAsync("/api/v1/servicos");
        listar.StatusCode.Should().Be(HttpStatusCode.OK);
        var pagina = await listar.Content.ReadFromJsonAsync<PaginaServicos>();
        pagina!.Itens.Should().NotContain(s => s.Id == criado.Id);
    }

    [Fact]
    public async Task Criar_ComPrecoZero_DeveRetornar422()
    {
        var http = await AutenticadoAsync();
        var resp = await http.PostAsJsonAsync("/api/v1/servicos",
            new CriarServicoRequest("X", "y", 0m, 30));
        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Listar_ComFiltroDeNome_DeveRetornarApenasServicosCorrespondentes()
    {
        var http = await AutenticadoAsync();

        await http.PostAsJsonAsync("/api/v1/servicos",
            new CriarServicoRequest("Pintura completa", "x", 1500m, 480));
        await http.PostAsJsonAsync("/api/v1/servicos",
            new CriarServicoRequest("Lavagem detalhada", "x", 80m, 90));

        var resp = await http.GetAsync("/api/v1/servicos?nome=pintura");
        var pagina = await resp.Content.ReadFromJsonAsync<PaginaServicos>();
        pagina!.Itens.Should().Contain(s => s.Nome.Contains("Pintura"));
        pagina.Itens.Should().NotContain(s => s.Nome.Contains("Lavagem"));
    }
}
