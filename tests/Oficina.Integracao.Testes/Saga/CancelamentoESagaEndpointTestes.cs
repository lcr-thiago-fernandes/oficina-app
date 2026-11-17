using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Oficina.Aplicacao.Catalogo.Dtos;
using Oficina.Aplicacao.Clientes.Dtos;
using Oficina.Aplicacao.Estoque.Dtos;
using Oficina.Aplicacao.OrdensServico.Dtos;
using Oficina.Aplicacao.OrdensServico.Saga.Dtos;
using Oficina.Dominio.OrdensServico;
using Oficina.Infraestrutura.Persistencia;
using Oficina.Integracao.Testes.Auth;

namespace Oficina.Integracao.Testes.Saga;

/// <summary>POST {id}/cancelar e GET {id}/saga contra a saga real sobre o bus in-memory do harness.</summary>
[Collection(nameof(AuthCollection))]
public class CancelamentoESagaEndpointTestes
{
    private const int SaldoInicial = 10;
    private const int QuantidadePeca = 2;
    private const string Motivo = "Cliente desistiu";

    private readonly AuthFixture _fx;

    public CancelamentoESagaEndpointTestes(AuthFixture fx) => _fx = fx;

    [Fact]
    public async Task Cancelar_OsRecebida_Retorna202EAsagaCancelaELiberaReserva()
    {
        var http = await AutenticadoAsync();
        var (os, pecaId) = await AbrirOrdemComPecaAsync(http);
        (await PecaAsync(http, pecaId)).SaldoReservado.Should().Be(QuantidadePeca);
        await AguardarAsync(async () => (await ObterSagaAsync(http, os.Id))?.Estado == "AguardandoOrcamento",
            TimeSpan.FromSeconds(10), "saga em AguardandoOrcamento");

        var resp = await http.PostAsJsonAsync($"/api/v1/ordens-servico/{os.Id}/cancelar", new CancelarOrdemRequest(Motivo));

        resp.StatusCode.Should().Be(HttpStatusCode.Accepted, await resp.Content.ReadAsStringAsync());
        (await resp.Content.ReadFromJsonAsync<OrdemResponse>())!.Id.Should().Be(os.Id);

        await AguardarAsync(async () => (await ObterSagaAsync(http, os.Id))?.Estado == "Cancelada",
            TimeSpan.FromSeconds(10), "saga Cancelada");
        await AguardarAsync(async () => (await ObterOrdemAsync(http, os.Id)).Status == "Cancelada",
            TimeSpan.FromSeconds(10), "OS Cancelada");
        var ordem = await ObterOrdemAsync(http, os.Id);
        ordem.MotivoCancelamento.Should().Be(Motivo);
        var peca = await PecaAsync(http, pecaId);
        peca.SaldoReservado.Should().Be(0);
        peca.SaldoAtual.Should().Be(SaldoInicial);

        var saga = (await ObterSagaAsync(http, os.Id))!;
        saga.Trilha.Should().NotBeEmpty();
        saga.Trilha.Last().Estado.Should().Be("Cancelada");
        saga.Trilha.Should().OnlyContain(p => !string.IsNullOrEmpty(p.Estado));
    }

    [Fact]
    public async Task Cancelar_OsLegadaSemSaga_Retorna200ECancelaNaHora()
    {
        var http = await AutenticadoAsync();
        var (os, pecaId) = await AbrirOrdemComPecaAsync(http);
        using (var scope = _fx.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OficinaDbContext>();
            await db.OrdensServico.Where(o => o.Id == os.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(o => o.CoordenadaPorSaga, false));
        }

        var resp = await http.PostAsJsonAsync($"/api/v1/ordens-servico/{os.Id}/cancelar", new CancelarOrdemRequest(Motivo));

        resp.StatusCode.Should().Be(HttpStatusCode.OK, await resp.Content.ReadAsStringAsync());
        var corpo = (await resp.Content.ReadFromJsonAsync<OrdemResponse>())!;
        corpo.Status.Should().Be("Cancelada");
        corpo.MotivoCancelamento.Should().Be(Motivo);
        (await PecaAsync(http, pecaId)).SaldoReservado.Should().Be(0);
    }

    [Fact]
    public async Task Cancelar_Inexistente_Retorna404()
    {
        var http = await AutenticadoAsync();

        var resp = await http.PostAsJsonAsync($"/api/v1/ordens-servico/{Guid.NewGuid()}/cancelar", new CancelarOrdemRequest(Motivo));

        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Cancelar_SemMotivo_Retorna400(string motivo)
    {
        var http = await AutenticadoAsync();
        var (os, _) = await AbrirOrdemComPecaAsync(http);

        var resp = await http.PostAsJsonAsync($"/api/v1/ordens-servico/{os.Id}/cancelar", new CancelarOrdemRequest(motivo));

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ObterOrdemAsync(http, os.Id)).Status.Should().Be("Recebida");
    }

    [Fact]
    public async Task Cancelar_CorpoSemMotivo_Retorna400()
    {
        var http = await AutenticadoAsync();
        var (os, _) = await AbrirOrdemComPecaAsync(http);

        var resp = await http.PostAsJsonAsync($"/api/v1/ordens-servico/{os.Id}/cancelar", new { });

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Cancelar_OsFinalizada_Retorna422()
    {
        var http = await AutenticadoAsync();
        var (os, _) = await AbrirOrdemComPecaAsync(http);
        await _fx.LevarOrdemAteStatusNoBancoAsync(os.Id, StatusOrdemDeServico.Finalizada);

        var resp = await http.PostAsJsonAsync($"/api/v1/ordens-servico/{os.Id}/cancelar", new CancelarOrdemRequest(Motivo));

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task ObterSaga_Inexistente_Retorna404()
    {
        var http = await AutenticadoAsync();

        (await http.GetAsync($"/api/v1/ordens-servico/{Guid.NewGuid()}/saga")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ObterSaga_SemToken_Retorna401()
    {
        var anonimo = _fx.Factory.CreateClient();

        (await anonimo.GetAsync($"/api/v1/ordens-servico/{Guid.NewGuid()}/saga")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anonimo.PostAsJsonAsync($"/api/v1/ordens-servico/{Guid.NewGuid()}/cancelar", new CancelarOrdemRequest(Motivo)))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task HistoricoDuracao_CancelamentoPelaSaga_GravaDuracao()
    {
        var http = await AutenticadoAsync();
        var (os, _) = await AbrirOrdemComPecaAsync(http);
        await AguardarAsync(async () => (await ObterSagaAsync(http, os.Id))?.Estado == "AguardandoOrcamento",
            TimeSpan.FromSeconds(10), "saga em AguardandoOrcamento");

        (await http.PostAsJsonAsync($"/api/v1/ordens-servico/{os.Id}/cancelar", new CancelarOrdemRequest(Motivo)))
            .StatusCode.Should().Be(HttpStatusCode.Accepted);
        await AguardarAsync(async () => (await ObterOrdemAsync(http, os.Id)).Status == "Cancelada",
            TimeSpan.FromSeconds(10), "OS Cancelada");

        using var scope = _fx.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OficinaDbContext>();
        var historico = await db.Set<HistoricoStatus>()
            .Where(h => EF.Property<Guid>(h, "ordem_servico_id") == os.Id)
            .OrderBy(h => h.OcorridoEm).ToListAsync();
        historico.Should().HaveCount(2);
        historico[0].StatusNovo.Should().Be(StatusOrdemDeServico.Recebida);
        historico[1].StatusNovo.Should().Be(StatusOrdemDeServico.Cancelada);
        historico[1].DuracaoSegundos.Should().NotBeNull();
    }

    // ---------------------------------------------------------------- helpers

    private async Task<HttpClient> AutenticadoAsync()
    {
        var http = _fx.Factory.CreateClient();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await _fx.ObterTokenAdminAsync());
        return http;
    }

    private static async Task<EstadoSagaResponse?> ObterSagaAsync(HttpClient http, Guid osId)
    {
        var resp = await http.GetAsync($"/api/v1/ordens-servico/{osId}/saga");
        return resp.StatusCode == HttpStatusCode.NotFound ? null : await resp.Content.ReadFromJsonAsync<EstadoSagaResponse>();
    }

    private static async Task<OrdemResponse> ObterOrdemAsync(HttpClient http, Guid osId) =>
        (await http.GetFromJsonAsync<OrdemResponse>($"/api/v1/ordens-servico/{osId}"))!;

    private static async Task<PecaResponse> PecaAsync(HttpClient http, Guid pecaId) =>
        (await http.GetFromJsonAsync<PecaResponse>($"/api/v1/pecas/{pecaId}"))!;

    private static async Task AguardarAsync(Func<Task<bool>> condicao, TimeSpan limite, string descricao)
    {
        var fim = DateTime.UtcNow + limite;
        while (DateTime.UtcNow < fim)
        {
            if (await condicao())
                return;
            await Task.Delay(100);
        }

        if (!await condicao())
            throw new TimeoutException($"Não aconteceu em {limite}: {descricao}.");
    }

    private static async Task<(OrdemResponse Os, Guid PecaId)> AbrirOrdemComPecaAsync(HttpClient http)
    {
        var servico = (await (await http.PostAsJsonAsync("/api/v1/servicos", new CriarServicoRequest("Troca de óleo", "x", 150m, 30)))
            .Content.ReadFromJsonAsync<ServicoResponse>())!;
        var peca = (await (await http.PostAsJsonAsync("/api/v1/pecas",
                new CriarPecaRequest($"CAN-{Guid.NewGuid():N}"[..12], "Filtro de óleo", 25m)))
            .Content.ReadFromJsonAsync<PecaResponse>())!;
        (await http.PostAsJsonAsync($"/api/v1/pecas/{peca.Id}/movimentacoes",
            new RegistrarMovimentacaoRequest("Entrada", SaldoInicial, "compra", null))).EnsureSuccessStatusCode();

        var resp = await http.PostAsJsonAsync("/api/v1/ordens-servico", new AbrirOrdemRequest(
            new ClienteDadosDto(DocumentoUnico(), "Cliente Cancelamento", $"c{Guid.NewGuid():N}@x.com", "11987654321"),
            new VeiculoDadosDto(PlacaUnica(), "Fiat", "Uno", 2020),
            new[] { new ItemServicoDto(servico.Id, 1) },
            new[] { new ItemPecaDto(peca.Id, QuantidadePeca) }));

        resp.StatusCode.Should().Be(HttpStatusCode.Created, await resp.Content.ReadAsStringAsync());
        return ((await resp.Content.ReadFromJsonAsync<OrdemResponse>())!, peca.Id);
    }

    /// <summary>CPF válido e distinto a cada chamada (os testes da coleção compartilham o banco).</summary>
    internal static string DocumentoUnico()
    {
        var d = new int[11];
        for (var i = 0; i < 9; i++) d[i] = Random.Shared.Next(0, 10);
        if (d.Take(9).Distinct().Count() == 1) d[0] = (d[0] + 1) % 10;
        d[9] = Digito(d, 9);
        d[10] = Digito(d, 10);
        return string.Concat(d);

        static int Digito(int[] n, int tamanho)
        {
            var soma = 0;
            for (var i = 0; i < tamanho; i++) soma += n[i] * (tamanho + 1 - i);
            var r = soma * 10 % 11;
            return r == 10 ? 0 : r;
        }
    }

    internal static string PlacaUnica() =>
        $"{(char)('A' + Random.Shared.Next(26))}{(char)('A' + Random.Shared.Next(26))}{(char)('A' + Random.Shared.Next(26))}{Random.Shared.Next(1000, 9999)}";
}
