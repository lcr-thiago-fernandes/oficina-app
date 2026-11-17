using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Oficina.Aplicacao.Catalogo.Dtos;
using Oficina.Aplicacao.Clientes.Dtos;
using Oficina.Aplicacao.Estoque.Dtos;
using Oficina.Aplicacao.OrdensServico.Dtos;
using Oficina.Dominio.OrdensServico;
using Oficina.Integracao.Testes.Auth;
using Xunit;

namespace Oficina.Integracao.Testes.OrdensServico;

[Collection(nameof(AuthCollection))]
public class OrdensServicoFluxoTestes
{
    private readonly AuthFixture _fx;
    public OrdensServicoFluxoTestes(AuthFixture fx) => _fx = fx;

    private async Task<HttpClient> AutenticadoAsync()
    {
        var http = _fx.Factory.CreateClient();
        var token = await _fx.ObterTokenAdminAsync();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return http;
    }

    /// <summary>Abre uma OS (Recebida) com um serviço, via API.</summary>
    private static async Task<OrdemResponse> AbrirOrdemAsync(HttpClient http)
    {
        var s = await (await http.PostAsJsonAsync("/api/v1/servicos",
            new CriarServicoRequest("X", "y", 10m, 10))).Content.ReadFromJsonAsync<ServicoResponse>();
        var resp = await http.PostAsJsonAsync("/api/v1/ordens-servico",
            new AbrirOrdemRequest(
                new ClienteDadosDto("11144477735", "Cli Fluxo", $"f{Guid.NewGuid():N}@x.com", "11987654321"),
                new VeiculoDadosDto($"FLX{new Random().Next(1000, 9999)}", "F", "U", 2020),
                new[] { new ItemServicoDto(s!.Id, 1) },
                Array.Empty<ItemPecaDto>()));
        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await resp.Content.ReadFromJsonAsync<OrdemResponse>())!;
    }

    [Fact]
    public async Task FluxoCompleto_AbrirLevarAteFinalizadaEEntregar_DeveTerminarEntregue()
    {
        var http = await AutenticadoAsync();

        // Serviço
        var servResp = await http.PostAsJsonAsync("/api/v1/servicos",
            new CriarServicoRequest("Troca de óleo", "x", 150m, 30));
        var servico = await servResp.Content.ReadFromJsonAsync<ServicoResponse>();

        // Peça com estoque inicial
        var pecaResp = await http.PostAsJsonAsync("/api/v1/pecas",
            new CriarPecaRequest($"FILT-{Guid.NewGuid():N}".Substring(0, 12), "Filtro", 25m));
        var peca = await pecaResp.Content.ReadFromJsonAsync<PecaResponse>();
        await http.PostAsJsonAsync($"/api/v1/pecas/{peca!.Id}/movimentacoes",
            new RegistrarMovimentacaoRequest("Entrada", 10, "compra", null));

        // Abrir OS consolidada (cliente/veículo find-or-create + serviço + peça)
        var osResp = await http.PostAsJsonAsync("/api/v1/ordens-servico",
            new AbrirOrdemRequest(
                new ClienteDadosDto("11144477735", "Cliente OS", $"c{Guid.NewGuid():N}@x.com", "11987654321"),
                new VeiculoDadosDto($"OSA{new Random().Next(1000, 9999)}", "Fiat", "Uno", 2020),
                new[] { new ItemServicoDto(servico!.Id, 1) },
                new[] { new ItemPecaDto(peca.Id, 3) },
                "obs"));
        osResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var os = await osResp.Content.ReadFromJsonAsync<OrdemResponse>();
        os!.Status.Should().Be("Recebida");

        // Orçamento, pagamento e execução chegam pela saga (Billing/Execução) — aqui, direto no banco.
        await _fx.LevarOrdemAteStatusNoBancoAsync(os.Id, StatusOrdemDeServico.Finalizada);

        var ent = await http.PostAsync($"/api/v1/ordens-servico/{os.Id}/entregar", null);
        ent.StatusCode.Should().Be(HttpStatusCode.OK);

        // Estado final
        var final = await http.GetAsync($"/api/v1/ordens-servico/{os.Id}");
        var finalOs = await final.Content.ReadFromJsonAsync<OrdemResponse>();
        finalOs!.Status.Should().Be("Entregue");
        finalOs.EntregueEm.Should().NotBeNull();
        finalOs.MotivoCancelamento.Should().BeNull();
        finalOs.CanceladaEm.Should().BeNull();
    }

    [Fact]
    public async Task AbrirOrdem_SemItens_DeveRetornar400()
    {
        var http = await AutenticadoAsync();

        var cliResp = await http.GetAsync("/api/v1/clientes?documento=11144477735");
        var cliente = cliResp.IsSuccessStatusCode
            ? await cliResp.Content.ReadFromJsonAsync<ClienteResponse>()
            : null;
        if (cliente is null)
        {
            var create = await http.PostAsJsonAsync("/api/v1/clientes",
                new CriarClienteRequest("X", "11144477735", $"x{Guid.NewGuid():N}@x.com", "11987654321"));
            cliente = await create.Content.ReadFromJsonAsync<ClienteResponse>();
        }
        var v = await http.PostAsJsonAsync($"/api/v1/clientes/{cliente!.Id}/veiculos",
            new AdicionarVeiculoRequest($"NEW{new Random().Next(1000, 9999)}", "F", "U", 2020));
        var veic = await v.Content.ReadFromJsonAsync<VeiculoResponse>();

        // Abrir OS sem nenhum servico e nenhuma peca: AbrirOrdemValidator rejeita.
        // A regra de dominio equivalente (OrdemSemItensException) tem cobertura
        // propria em Oficina.Dominio.Testes/OrdensServico/OrdemDeServicoTestes.cs.
        var resp = await http.PostAsJsonAsync("/api/v1/ordens-servico",
            new AbrirOrdemRequest(
                new ClienteDadosDto("11144477735", "X", $"x{Guid.NewGuid():N}@x.com", "11987654321"),
                new VeiculoDadosDto(veic!.Placa, "F", "U", 2020),
                Array.Empty<ItemServicoDto>(),
                Array.Empty<ItemPecaDto>()));

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Entregar_ForaDeFinalizada_DeveRetornar422()
    {
        var http = await AutenticadoAsync();
        var os = await AbrirOrdemAsync(http);
        await _fx.LevarOrdemAteStatusNoBancoAsync(os.Id, StatusOrdemDeServico.EmReparo);

        var resp = await http.PostAsync($"/api/v1/ordens-servico/{os.Id}/entregar", null);

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Theory]
    [InlineData("PATCH", "diagnostico")] [InlineData("POST", "orcamento/enviar")] [InlineData("POST", "execucao/iniciar")]
    [InlineData("POST", "finalizar")]    [InlineData("POST", "orcamento/aprovacao")] [InlineData("POST", "servicos")]
    public async Task RotasMigradasParaOutrosServicos_NaoExistemMais(string metodo, string sufixo)
    {
        var http = await AutenticadoAsync();
        var os = await AbrirOrdemAsync(http);

        var req = new HttpRequestMessage(new HttpMethod(metodo), $"/api/v1/ordens-servico/{os.Id}/{sufixo}")
        {
            Content = JsonContent.Create(new { })
        };
        var resp = await http.SendAsync(req);

        resp.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed);
    }
}
