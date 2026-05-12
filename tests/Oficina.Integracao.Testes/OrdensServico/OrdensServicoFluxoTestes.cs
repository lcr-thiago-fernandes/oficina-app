using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Oficina.Aplicacao.Catalogo.Dtos;
using Oficina.Aplicacao.Clientes.Dtos;
using Oficina.Aplicacao.Estoque.Dtos;
using Oficina.Aplicacao.OrdensServico.Dtos;
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

    private static string DocumentoUnico() => "11144477735"; // CPF válido fixo
    // (em testes reais, gerar dinâmico com algoritmo de geração — para MVP basta fixar)

    [Fact]
    public async Task FluxoCompleto_DeveAvancarPelosEstadosEBaixarEstoque()
    {
        var http = await AutenticadoAsync();

        // Cliente
        var clienteResp = await http.PostAsJsonAsync("/api/v1/clientes",
            new CriarClienteRequest("Cliente OS", "11144477735",
                $"c{Guid.NewGuid():N}@x.com", "11987654321"));
        var cliente = await clienteResp.Content.ReadFromJsonAsync<ClienteResponse>();
        clienteResp.StatusCode.Should().BeOneOf(HttpStatusCode.Created, HttpStatusCode.Conflict);
        if (clienteResp.StatusCode == HttpStatusCode.Conflict)
        {
            var get = await http.GetAsync("/api/v1/clientes?documento=11144477735");
            cliente = await get.Content.ReadFromJsonAsync<ClienteResponse>();
        }

        // Veículo
        var veicResp = await http.PostAsJsonAsync($"/api/v1/clientes/{cliente!.Id}/veiculos",
            new AdicionarVeiculoRequest($"OS{new Random().Next(1000, 9999)}A11", "Fiat", "Uno", 2020));
        var veiculo = await veicResp.Content.ReadFromJsonAsync<VeiculoResponse>();

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

        // Criar OS
        var osResp = await http.PostAsJsonAsync("/api/v1/ordens-servico",
            new CriarOrdemRequest(cliente.Id, veiculo!.Id, "obs"));
        osResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var os = await osResp.Content.ReadFromJsonAsync<OrdemResponse>();

        // Diagnóstico
        var diag = await http.PatchAsync($"/api/v1/ordens-servico/{os!.Id}/diagnostico", null);
        diag.StatusCode.Should().Be(HttpStatusCode.OK);

        // Adicionar serviço
        var addS = await http.PostAsJsonAsync($"/api/v1/ordens-servico/{os.Id}/servicos",
            new AdicionarItemServicoRequest(servico!.Id, 1));
        addS.StatusCode.Should().Be(HttpStatusCode.Created);

        // Adicionar peça
        var addP = await http.PostAsJsonAsync($"/api/v1/ordens-servico/{os.Id}/pecas",
            new AdicionarItemPecaRequest(peca.Id, 3));
        addP.StatusCode.Should().Be(HttpStatusCode.Created);

        // Enviar para aprovação
        var enviar = await http.PostAsync($"/api/v1/ordens-servico/{os.Id}/orcamento/enviar", null);
        enviar.StatusCode.Should().Be(HttpStatusCode.OK);

        // Aprovar (helper direto no banco — Plano 7 implementa rota pública)
        await _fx.AprovarOrcamentoDiretoNoBancoAsync(os.Id);

        // Iniciar execução — deve baixar estoque
        var exec = await http.PostAsync($"/api/v1/ordens-servico/{os.Id}/execucao/iniciar", null);
        exec.StatusCode.Should().Be(HttpStatusCode.OK);

        var pecaAtualResp = await http.GetAsync($"/api/v1/pecas/{peca.Id}");
        var pecaAtual = await pecaAtualResp.Content.ReadFromJsonAsync<PecaResponse>();
        pecaAtual!.SaldoAtual.Should().Be(7);

        // Finalizar e entregar
        var fin = await http.PostAsync($"/api/v1/ordens-servico/{os.Id}/finalizar", null);
        fin.StatusCode.Should().Be(HttpStatusCode.OK);
        var ent = await http.PostAsync($"/api/v1/ordens-servico/{os.Id}/entregar", null);
        ent.StatusCode.Should().Be(HttpStatusCode.OK);

        // Estado final
        var final = await http.GetAsync($"/api/v1/ordens-servico/{os.Id}");
        var final_os = await final.Content.ReadFromJsonAsync<OrdemResponse>();
        final_os!.Status.Should().Be("Entregue");
    }

    [Fact]
    public async Task EnviarParaAprovacao_SemItens_DeveRetornar422()
    {
        var http = await AutenticadoAsync();

        // criar dependências mínimas
        var cliResp = await http.GetAsync("/api/v1/clientes?documento=11144477735");
        var cliente = cliResp.StatusCode == HttpStatusCode.OK
            ? await cliResp.Content.ReadFromJsonAsync<ClienteResponse>()
            : null;
        if (cliente is null)
        {
            var create = await http.PostAsJsonAsync("/api/v1/clientes",
                new CriarClienteRequest("X", "11144477735", $"x{Guid.NewGuid():N}@x.com", "11987654321"));
            cliente = await create.Content.ReadFromJsonAsync<ClienteResponse>();
        }
        var v = await http.PostAsJsonAsync($"/api/v1/clientes/{cliente!.Id}/veiculos",
            new AdicionarVeiculoRequest($"NEW{new Random().Next(1000,9999)}", "F", "U", 2020));
        var veic = await v.Content.ReadFromJsonAsync<VeiculoResponse>();

        var os = await http.PostAsJsonAsync("/api/v1/ordens-servico",
            new CriarOrdemRequest(cliente.Id, veic!.Id, null));
        var osR = await os.Content.ReadFromJsonAsync<OrdemResponse>();
        await http.PatchAsync($"/api/v1/ordens-servico/{osR!.Id}/diagnostico", null);

        var resp = await http.PostAsync($"/api/v1/ordens-servico/{osR.Id}/orcamento/enviar", null);
        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task IniciarExecucao_SemAprovacao_DeveRetornar422()
    {
        var http = await AutenticadoAsync();

        // OS já com itens e enviada para aprovação (sem aprovar)
        var cli = await (await http.GetAsync("/api/v1/clientes?documento=11144477735"))
            .Content.ReadFromJsonAsync<ClienteResponse>();
        var v = await (await http.PostAsJsonAsync($"/api/v1/clientes/{cli!.Id}/veiculos",
            new AdicionarVeiculoRequest($"WAI{new Random().Next(100,999)}A11", "F", "U", 2020)))
            .Content.ReadFromJsonAsync<VeiculoResponse>();
        var s = await (await http.PostAsJsonAsync("/api/v1/servicos",
            new CriarServicoRequest("X", "y", 10m, 10))).Content.ReadFromJsonAsync<ServicoResponse>();
        var os = await (await http.PostAsJsonAsync("/api/v1/ordens-servico",
            new CriarOrdemRequest(cli.Id, v!.Id, null))).Content.ReadFromJsonAsync<OrdemResponse>();
        await http.PatchAsync($"/api/v1/ordens-servico/{os!.Id}/diagnostico", null);
        await http.PostAsJsonAsync($"/api/v1/ordens-servico/{os.Id}/servicos",
            new AdicionarItemServicoRequest(s!.Id, 1));
        await http.PostAsync($"/api/v1/ordens-servico/{os.Id}/orcamento/enviar", null);

        var resp = await http.PostAsync($"/api/v1/ordens-servico/{os.Id}/execucao/iniciar", null);
        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }
}
