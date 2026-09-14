using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
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

    /// <summary>
    /// Busca o cliente pelo documento e só cria se a busca confirmar que ele
    /// não existe (404). Importante checar o StatusCode explicitamente: com
    /// [ApiController], um 404 "cru" vira ProblemDetails no corpo, e nenhum
    /// campo dele colide com ClienteResponse — um ReadFromJsonAsync direto,
    /// sem checar sucesso, desserializaria isso como um ClienteResponse
    /// "zerado" (Id = Guid.Empty) em vez de null, mascarando o 404 e
    /// quebrando tudo mais adiante (o teste falha em outro lugar, com uma
    /// mensagem que não aponta para a causa real). Isso tornava a suíte
    /// correta apenas por sorte de ordem entre classes que compartilham o
    /// mesmo CPF fixo — corrigido aqui para ser correta por construção,
    /// independente de quem rodou antes.
    /// </summary>
    private static async Task<ClienteResponse> ObterOuCriarClienteAsync(HttpClient http, string doc, string nome)
    {
        var busca = await http.GetAsync($"/api/v1/clientes?documento={doc}");
        if (busca.StatusCode == HttpStatusCode.OK)
            return (await busca.Content.ReadFromJsonAsync<ClienteResponse>())!;

        var criado = await http.PostAsJsonAsync("/api/v1/clientes",
            new CriarClienteRequest(nome, doc, $"c{Guid.NewGuid():N}@x.com", "11987654321"));
        return (await criado.Content.ReadFromJsonAsync<ClienteResponse>())!;
    }

    private async Task<(long numero, string documento)> CriarOsParaConsultaAsync()
    {
        var http = await AdminAsync();
        var doc = "11144477735";

        var cli = await ObterOuCriarClienteAsync(http, doc, "Cli OS");
        var v = await (await http.PostAsJsonAsync($"/api/v1/clientes/{cli.Id}/veiculos",
            new AdicionarVeiculoRequest($"OSC{new Random().Next(1000,9999)}", "F", "U", 2020)))
            .Content.ReadFromJsonAsync<VeiculoResponse>();

        var s = await (await http.PostAsJsonAsync("/api/v1/servicos",
            new CriarServicoRequest("S consulta", "x", 10m, 10)))
            .Content.ReadFromJsonAsync<ServicoResponse>();

        var os = await (await http.PostAsJsonAsync("/api/v1/ordens-servico",
            new AbrirOrdemRequest(
                new ClienteDadosDto(doc, "Cli OS", $"c{Guid.NewGuid():N}@x.com", "11987654321"),
                new VeiculoDadosDto(v!.Placa, "F", "U", 2020),
                new[] { new ItemServicoDto(s!.Id, 1) },
                Array.Empty<ItemPecaDto>()))).Content.ReadFromJsonAsync<OrdemResponse>();
        await http.PatchAsync($"/api/v1/ordens-servico/{os!.Id}/diagnostico", null);
        await http.PostAsync($"/api/v1/ordens-servico/{os.Id}/orcamento/enviar", null);

        return (os.Numero, doc);
    }

    [Fact]
    public async Task Consulta_da_propria_OS_retorna_200()
    {
        var (numero, doc) = await CriarOsParaConsultaAsync();

        var http = _fx.Factory.CreateClient();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", await _fx.ObterTokenClienteAsync(Guid.NewGuid(), doc));

        var resp = await http.GetAsync($"/api/v1/consulta/{numero}");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadFromJsonAsync<ConsultaPublicaResponse>();
        body!.Numero.Should().Be(numero);
        body.Status.Should().Be("AguardandoAprovacao");
        body.DocumentoMascarado.Should().NotContain("***");
    }

    [Fact]
    public async Task Consulta_de_OS_de_outro_cliente_retorna_404()
    {
        var (numero, _) = await CriarOsParaConsultaAsync();

        // Cliente valido, porem dono de outro documento.
        var http = _fx.Factory.CreateClient();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", await _fx.ObterTokenClienteAsync(Guid.NewGuid(), "52998224725"));

        var resp = await http.GetAsync($"/api/v1/consulta/{numero}");

        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Consulta_de_OS_inexistente_retorna_404()
    {
        var http = _fx.Factory.CreateClient();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", await _fx.ObterTokenClienteAsync(Guid.NewGuid(), "11144477735"));

        var resp = await http.GetAsync("/api/v1/consulta/9999999");

        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Consulta_sem_token_retorna_401()
    {
        var (numero, _) = await CriarOsParaConsultaAsync();

        var http = _fx.Factory.CreateClient();
        var resp = await http.GetAsync($"/api/v1/consulta/{numero}");

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Consulta_de_OS_inexistente_e_de_outro_cliente_retornam_corpo_identico()
    {
        // A invariante anti-enumeração não é só o StatusCode — é o corpo da
        // resposta ser indistinguível entre "não existe" e "existe, mas não
        // é seu". Um futuro NotFound(new { erro = "..." }) só no ramo
        // DocumentoNaoConfere passaria despercebido pelos outros testes
        // (que só checam o status) e reabriria o oráculo de enumeração.
        var (numero, _) = await CriarOsParaConsultaAsync();

        var http = _fx.Factory.CreateClient();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", await _fx.ObterTokenClienteAsync(Guid.NewGuid(), "52998224725"));

        var respOutroCliente = await http.GetAsync($"/api/v1/consulta/{numero}");
        var respInexistente = await http.GetAsync("/api/v1/consulta/9999999");

        respOutroCliente.StatusCode.Should().Be(HttpStatusCode.NotFound);
        respInexistente.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // [ApiController] converte o NotFound() cru em ProblemDetails, que
        // inclui um traceId novo a cada requisição — normalizamos esse único
        // campo antes de comparar o resto do corpo byte a byte.
        var corpoOutroCliente = NormalizarTraceId(await respOutroCliente.Content.ReadAsStringAsync());
        var corpoInexistente = NormalizarTraceId(await respInexistente.Content.ReadAsStringAsync());

        corpoOutroCliente.Should().Be(corpoInexistente);
    }

    private static string NormalizarTraceId(string json) =>
        Regex.Replace(json, "\"traceId\"\\s*:\\s*\"[^\"]*\"", "\"traceId\":\"\"");

    private async Task<OrdemResponse> CriarOsEnviadaAsync()
    {
        var http = await AdminAsync();
        var doc = "11144477735";

        var cli = await ObterOuCriarClienteAsync(http, doc, "Cli OS");
        var v = await (await http.PostAsJsonAsync($"/api/v1/clientes/{cli.Id}/veiculos",
            new AdicionarVeiculoRequest($"WHK{new Random().Next(1000,9999)}", "F", "U", 2020)))
            .Content.ReadFromJsonAsync<VeiculoResponse>();
        var s = await (await http.PostAsJsonAsync("/api/v1/servicos",
            new CriarServicoRequest("S webhook", "x", 10m, 10)))
            .Content.ReadFromJsonAsync<ServicoResponse>();

        var os = await (await http.PostAsJsonAsync("/api/v1/ordens-servico",
            new AbrirOrdemRequest(
                new ClienteDadosDto(doc, "Cli OS", $"c{Guid.NewGuid():N}@x.com", "11987654321"),
                new VeiculoDadosDto(v!.Placa, "F", "U", 2020),
                new[] { new ItemServicoDto(s!.Id, 1) },
                Array.Empty<ItemPecaDto>()))).Content.ReadFromJsonAsync<OrdemResponse>();
        await http.PatchAsync($"/api/v1/ordens-servico/{os!.Id}/diagnostico", null);
        await http.PostAsync($"/api/v1/ordens-servico/{os.Id}/orcamento/enviar", null);
        return os;
    }

    [Fact]
    public async Task Webhook_ComTokenValido_Aprovado_DeveRetornar200()
    {
        var os = await CriarOsEnviadaAsync();
        var publico = _fx.Factory.CreateClient();
        publico.DefaultRequestHeaders.Add("X-Webhook-Token", "token-teste-webhook");

        var resp = await publico.PostAsJsonAsync(
            $"/api/v1/ordens-servico/{os.Id}/orcamento/aprovacao",
            new DecisaoOrcamentoRequest("aprovado"));

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadFromJsonAsync<OrdemResponse>();
        body!.OrcamentoAprovadoEm.Should().NotBeNull();
    }

    [Fact]
    public async Task Webhook_SemToken_DeveRetornar401()
    {
        var os = await CriarOsEnviadaAsync();
        var publico = _fx.Factory.CreateClient();

        var resp = await publico.PostAsJsonAsync(
            $"/api/v1/ordens-servico/{os.Id}/orcamento/aprovacao",
            new DecisaoOrcamentoRequest("aprovado"));

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Webhook_SemTokenESemBody_DeveRetornar401_NaoBadRequest()
    {
        // Regressão: com [ApiController], a auto-validação do [FromBody] roda
        // como filtro ANTES do corpo da action. Sem o ValidacaoTokenWebhookFilter
        // (IAuthorizationFilter, que roda ANTES do model binding), uma requisição
        // sem body/Content-Type retornaria 400/415 em vez de 401 — vazando para um
        // atacante sem token a informação de que o recurso existe/aceita a rota.
        var os = await CriarOsEnviadaAsync();
        var publico = _fx.Factory.CreateClient();

        using var req = new HttpRequestMessage(
            HttpMethod.Post, $"/api/v1/ordens-servico/{os.Id}/orcamento/aprovacao");
        // Sem header X-Webhook-Token e sem body/Content-Type algum.
        var resp = await publico.SendAsync(req);

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Webhook_DecisaoInvalida_DeveRetornar422()
    {
        var os = await CriarOsEnviadaAsync();
        var publico = _fx.Factory.CreateClient();
        publico.DefaultRequestHeaders.Add("X-Webhook-Token", "token-teste-webhook");

        var resp = await publico.PostAsJsonAsync(
            $"/api/v1/ordens-servico/{os.Id}/orcamento/aprovacao",
            new DecisaoOrcamentoRequest("talvez"));

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }
}
