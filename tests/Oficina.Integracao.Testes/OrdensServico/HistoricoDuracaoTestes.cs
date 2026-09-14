using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Oficina.Aplicacao.Catalogo.Dtos;
using Oficina.Aplicacao.Clientes.Dtos;
using Oficina.Aplicacao.OrdensServico.Dtos;
using Oficina.Dominio.OrdensServico;
using Oficina.Infraestrutura.Persistencia;
using Oficina.Integracao.Testes.Auth;
using Xunit;

namespace Oficina.Integracao.Testes.OrdensServico;

/// <summary>
/// Regressão do Achado 4 (rodada de correção 1 da Task 3): duracao_segundos nascia sempre
/// NULL em produção porque ObterPorIdAsync não incluía Historico, então RegistrarTransicao
/// nunca encontrava uma entrada anterior para calcular a diferença. Isso esvaziava o
/// propósito da tabela historico_status, que existe para alimentar o dashboard "tempo médio
/// por status". Este teste exercita a transição via API (o mesmo caminho de produção) e
/// confirma, direto no banco, que a segunda entrada do histórico tem duração não-nula.
/// </summary>
[Collection(nameof(AuthCollection))]
public class HistoricoDuracaoTestes
{
    private readonly AuthFixture _fx;
    public HistoricoDuracaoTestes(AuthFixture fx) => _fx = fx;

    [Fact]
    public async Task Segunda_transicao_de_status_via_api_grava_duracao_nao_nula()
    {
        var http = _fx.Factory.CreateClient();
        var token = await _fx.ObterTokenAdminAsync();
        http.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        const string documento = "11144477735"; // CPF válido fixo, mesmo usado nos demais testes de fluxo
        var clienteResp = await http.PostAsJsonAsync("/api/v1/clientes",
            new CriarClienteRequest("Cliente Duracao", documento,
                $"d{Guid.NewGuid():N}@x.com", "11987654321"));
        var cliente = clienteResp.StatusCode == System.Net.HttpStatusCode.Conflict
            ? await (await http.GetAsync($"/api/v1/clientes?documento={documento}")).Content.ReadFromJsonAsync<ClienteResponse>()
            : await clienteResp.Content.ReadFromJsonAsync<ClienteResponse>();

        var veiculo = await (await http.PostAsJsonAsync($"/api/v1/clientes/{cliente!.Id}/veiculos",
            new AdicionarVeiculoRequest($"DUR{new Random().Next(1000, 9999)}", "Fiat", "Uno", 2020)))
            .Content.ReadFromJsonAsync<VeiculoResponse>();

        var servico = await (await http.PostAsJsonAsync("/api/v1/servicos",
            new CriarServicoRequest("Servico Duracao", "x", 100m, 30)))
            .Content.ReadFromJsonAsync<ServicoResponse>();

        // Abrir OS (cria a 1a entrada de historico: Recebida, duracao null por ser a primeira)
        var osResp = await http.PostAsJsonAsync("/api/v1/ordens-servico",
            new AbrirOrdemRequest(
                new ClienteDadosDto(documento, "Cliente Duracao", $"d{Guid.NewGuid():N}@x.com", "11987654321"),
                new VeiculoDadosDto(veiculo!.Placa, "Fiat", "Uno", 2020),
                new[] { new ItemServicoDto(servico!.Id, 1) },
                Array.Empty<ItemPecaDto>(),
                null));
        osResp.StatusCode.Should().Be(System.Net.HttpStatusCode.Created);
        var os = await osResp.Content.ReadFromJsonAsync<OrdemResponse>();

        // Segunda transicao via API — Recebida -> EmDiagnostico (2a entrada de historico)
        var diag = await http.PatchAsync($"/api/v1/ordens-servico/{os!.Id}/diagnostico", null);
        diag.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);

        using var scope = _fx.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OficinaDbContext>();
        var historico = await db.Set<HistoricoStatus>()
            .Where(h => EF.Property<Guid>(h, "ordem_servico_id") == os.Id)
            .OrderBy(h => h.OcorridoEm)
            .ToListAsync();

        historico.Should().HaveCount(2);
        historico[0].StatusNovo.Should().Be(StatusOrdemDeServico.Recebida);
        historico[0].DuracaoSegundos.Should().BeNull("é a primeira entrada, não há transição anterior para medir");

        historico[1].StatusNovo.Should().Be(StatusOrdemDeServico.EmDiagnostico);
        historico[1].DuracaoSegundos.Should().NotBeNull(
            "ObterPorIdAsync agora inclui Historico, entao RegistrarTransicao encontra a entrada anterior");
        historico[1].DuracaoSegundos!.Value.Should().BeGreaterThanOrEqualTo(0);
    }
}
