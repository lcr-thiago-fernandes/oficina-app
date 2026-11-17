using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Oficina.Aplicacao.Catalogo.Dtos;
using Oficina.Aplicacao.Clientes.Dtos;
using Oficina.Aplicacao.Estoque.Dtos;
using Oficina.Aplicacao.OrdensServico.Dtos;
using Oficina.Integracao.Testes.Auth;
using Oficina.Integracao.Testes.Saga;

namespace Oficina.Integracao.Testes.Estoque;

[Collection(nameof(AuthCollection))]
public class ReservaConcorrenteTestes
{
    private readonly AuthFixture _fx;

    public ReservaConcorrenteTestes(AuthFixture fx) => _fx = fx;

    [Fact]
    public async Task AbrirOrdens_Concorrentes_PelaUltimaUnidade_SoUmaReserva()
    {
        var http = await AutenticadoAsync();
        var servico = await CriarServicoAsync(http);
        var peca = await CriarPecaComSaldoAsync(http, 1);
        // Dois clientes distintos (documento e placa): a disputa é só pela peça.
        var req1 = NovaOrdem(servico.Id, peca.Id, 1);
        var req2 = NovaOrdem(servico.Id, peca.Id, 1);

        var respostas = await Task.WhenAll(
            http.PostAsJsonAsync("/api/v1/ordens-servico", req1),
            http.PostAsJsonAsync("/api/v1/ordens-servico", req2));

        var codigos = respostas.Select(r => r.StatusCode).ToList();
        codigos.Count(c => c == HttpStatusCode.Created).Should().Be(1, string.Join(", ", codigos));
        // 409: perdeu a corrida (40001/xmin); 422: leu o saldo já reservado.
        codigos.Single(c => c != HttpStatusCode.Created).Should().BeOneOf(HttpStatusCode.Conflict, HttpStatusCode.UnprocessableEntity);

        var final = await ObterPecaAsync(http, peca.Id);
        final.SaldoAtual.Should().Be(1);
        final.SaldoReservado.Should().Be(1);
    }

    [Fact]
    public async Task AbrirOrdem_SaldoInsuficiente_Retorna422()
    {
        var http = await AutenticadoAsync();
        var servico = await CriarServicoAsync(http);
        var peca = await CriarPecaComSaldoAsync(http, 1);

        var resp = await http.PostAsJsonAsync("/api/v1/ordens-servico", NovaOrdem(servico.Id, peca.Id, 2));

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ObterPecaAsync(http, peca.Id)).SaldoReservado.Should().Be(0);
    }

    [Fact]
    public async Task SaidaManual_SobreEstoqueReservado_Retorna422()
    {
        var http = await AutenticadoAsync();
        var servico = await CriarServicoAsync(http);
        var peca = await CriarPecaComSaldoAsync(http, 3);
        (await http.PostAsJsonAsync("/api/v1/ordens-servico", NovaOrdem(servico.Id, peca.Id, 3)))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        var resp = await http.PostAsJsonAsync($"/api/v1/pecas/{peca.Id}/movimentacoes",
            new RegistrarMovimentacaoRequest("Saida", 1, "balcao", null));

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var final = await ObterPecaAsync(http, peca.Id);
        final.SaldoAtual.Should().Be(3);
        final.SaldoReservado.Should().Be(3);
    }

    private async Task<HttpClient> AutenticadoAsync()
    {
        var http = _fx.Factory.CreateClient();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await _fx.ObterTokenAdminAsync());
        return http;
    }

    private static async Task<ServicoResponse> CriarServicoAsync(HttpClient http) =>
        (await (await http.PostAsJsonAsync("/api/v1/servicos", new CriarServicoRequest("Troca de óleo", "x", 150m, 30)))
            .Content.ReadFromJsonAsync<ServicoResponse>())!;

    private static async Task<PecaResponse> CriarPecaComSaldoAsync(HttpClient http, int saldo)
    {
        var peca = (await (await http.PostAsJsonAsync("/api/v1/pecas",
                new CriarPecaRequest($"RES-{Guid.NewGuid():N}"[..12], "Filtro de óleo", 25m)))
            .Content.ReadFromJsonAsync<PecaResponse>())!;
        (await http.PostAsJsonAsync($"/api/v1/pecas/{peca.Id}/movimentacoes",
            new RegistrarMovimentacaoRequest("Entrada", saldo, "compra", null))).EnsureSuccessStatusCode();
        return peca;
    }

    private static async Task<PecaResponse> ObterPecaAsync(HttpClient http, Guid id) =>
        (await http.GetFromJsonAsync<PecaResponse>($"/api/v1/pecas/{id}"))!;

    private static AbrirOrdemRequest NovaOrdem(Guid servicoId, Guid pecaId, int quantidade) =>
        new(
            new ClienteDadosDto(CancelamentoESagaEndpointTestes.DocumentoUnico(), "Cliente Reserva", $"r{Guid.NewGuid():N}@x.com", "11987654321"),
            new VeiculoDadosDto(CancelamentoESagaEndpointTestes.PlacaUnica(), "Fiat", "Uno", 2020),
            new[] { new ItemServicoDto(servicoId, 1) },
            new[] { new ItemPecaDto(pecaId, quantidade) });
}
