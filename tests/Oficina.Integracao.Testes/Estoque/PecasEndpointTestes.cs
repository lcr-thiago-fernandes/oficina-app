using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Oficina.Aplicacao.Estoque;
using Oficina.Aplicacao.Estoque.Dtos;
using Oficina.Integracao.Testes.Auth;
using Xunit;

namespace Oficina.Integracao.Testes.Estoque;

[Collection(nameof(AuthCollection))]
public class PecasEndpointTestes
{
    private readonly AuthFixture _fx;
    public PecasEndpointTestes(AuthFixture fx) => _fx = fx;

    private async Task<HttpClient> AutenticadoAsync()
    {
        var http = _fx.Factory.CreateClient();
        var token = await _fx.ObterTokenAdminAsync();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return http;
    }

    private static string SkuUnico() => $"SKU-{Guid.NewGuid():N}".Substring(0, 12);

    [Fact]
    public async Task FluxoCompleto_CriarMovimentarERemover()
    {
        var http = await AutenticadoAsync();
        var sku = SkuUnico();

        // Criar
        var criar = await http.PostAsJsonAsync("/api/v1/pecas",
            new CriarPecaRequest(sku, "Filtro", 25m));
        criar.StatusCode.Should().Be(HttpStatusCode.Created);
        var p = await criar.Content.ReadFromJsonAsync<PecaResponse>();
        p!.SaldoAtual.Should().Be(0);

        // Entrada
        var entrada = await http.PostAsJsonAsync($"/api/v1/pecas/{p.Id}/movimentacoes",
            new RegistrarMovimentacaoRequest("Entrada", 10, "Compra", null));
        entrada.StatusCode.Should().Be(HttpStatusCode.Created);

        // Verificar saldo
        var get1 = await http.GetAsync($"/api/v1/pecas/{p.Id}");
        var p1 = await get1.Content.ReadFromJsonAsync<PecaResponse>();
        p1!.SaldoAtual.Should().Be(10);

        // Saída
        var saida = await http.PostAsJsonAsync($"/api/v1/pecas/{p.Id}/movimentacoes",
            new RegistrarMovimentacaoRequest("Saida", 3, "OS Teste", Guid.NewGuid()));
        saida.StatusCode.Should().Be(HttpStatusCode.Created);

        var get2 = await http.GetAsync($"/api/v1/pecas/{p.Id}");
        var p2 = await get2.Content.ReadFromJsonAsync<PecaResponse>();
        p2!.SaldoAtual.Should().Be(7);

        // Listar movimentações
        var listMov = await http.GetAsync($"/api/v1/pecas/{p.Id}/movimentacoes");
        var movs = await listMov.Content.ReadFromJsonAsync<List<MovimentacaoResponse>>();
        movs!.Should().HaveCount(2);
    }

    [Fact]
    public async Task Saida_AlemDoSaldo_DeveRetornar422()
    {
        var http = await AutenticadoAsync();
        var sku = SkuUnico();

        var criar = await http.PostAsJsonAsync("/api/v1/pecas",
            new CriarPecaRequest(sku, "X", 5m));
        var p = await criar.Content.ReadFromJsonAsync<PecaResponse>();

        var resp = await http.PostAsJsonAsync($"/api/v1/pecas/{p!.Id}/movimentacoes",
            new RegistrarMovimentacaoRequest("Saida", 1, "tentativa", null));
        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Criar_ComSkuDuplicado_DeveRetornar409()
    {
        var http = await AutenticadoAsync();
        var sku = SkuUnico();

        await http.PostAsJsonAsync("/api/v1/pecas", new CriarPecaRequest(sku, "X", 1m));

        var resp = await http.PostAsJsonAsync("/api/v1/pecas",
            new CriarPecaRequest(sku, "Y", 2m));
        resp.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task ListarComFiltro_DeveRetornarOk()
    {
        var http = await AutenticadoAsync();
        await http.PostAsJsonAsync("/api/v1/pecas",
            new CriarPecaRequest(SkuUnico(), "Pastilha de freio dianteira", 80m));

        var resp = await http.GetAsync("/api/v1/pecas?nome=pastilha");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var pagina = await resp.Content.ReadFromJsonAsync<PaginaPecas>();
        pagina!.Itens.Should().Contain(p => p.Nome.Contains("Pastilha"));
    }
}
