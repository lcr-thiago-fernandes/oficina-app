using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Oficina.Aplicacao.Catalogo.Dtos;
using Oficina.Aplicacao.Clientes.Dtos;
using Oficina.Aplicacao.Estoque.Dtos;
using Oficina.Aplicacao.OrdensServico.Dtos;
using Oficina.Infraestrutura.Mensageria.Contratos;
using Oficina.Infraestrutura.Mensageria.Internos;
using Oficina.Integracao.Testes.Auth;
using Xunit;

namespace Oficina.Integracao.Testes.Observabilidade;

/// <summary>
/// Bus outbox real do EF: a entrega acontece depois, em outro escopo e sem HttpContext. O X-Correlation-Id
/// precisa estar na mensagem desde o Publish (HTTP) e desde o Send da saga.
/// </summary>
[Collection(nameof(AuthCollection))]
public class CorrelacaoSagaTestes
{
    private readonly AuthFixture _fx;

    public CorrelacaoSagaTestes(AuthFixture fx) => _fx = fx;

    [Fact]
    public async Task AberturaViaHttp_ComXCorrelationId_ChegaAoGerarOrcamentoEntreguePeloOutbox()
    {
        var http = _fx.Factory.CreateClient();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await _fx.ObterTokenAdminAsync());
        var servico = (await (await http.PostAsJsonAsync("/api/v1/servicos", new CriarServicoRequest("Troca de óleo", "x", 150m, 30)))
            .Content.ReadFromJsonAsync<ServicoResponse>())!;
        var peca = (await (await http.PostAsJsonAsync("/api/v1/pecas",
                new CriarPecaRequest($"COR-{Guid.NewGuid():N}"[..12], "Filtro", 25m)))
            .Content.ReadFromJsonAsync<PecaResponse>())!;
        (await http.PostAsJsonAsync($"/api/v1/pecas/{peca.Id}/movimentacoes",
            new RegistrarMovimentacaoRequest("Entrada", 10, "compra", null))).EnsureSuccessStatusCode();
        var correlacao = "corr-" + Guid.NewGuid().ToString("N");

        var pedido = new HttpRequestMessage(HttpMethod.Post, "/api/v1/ordens-servico")
        {
            Content = JsonContent.Create(new AbrirOrdemRequest(
                new ClienteDadosDto("11144477735", "Cliente Correlacao", $"c{Guid.NewGuid():N}@x.com", "11987654321"),
                new VeiculoDadosDto($"COR{Random.Shared.Next(1000, 9999)}", "Fiat", "Uno", 2020),
                new[] { new ItemServicoDto(servico.Id, 1) },
                new[] { new ItemPecaDto(peca.Id, 1) })),
        };
        pedido.Headers.Add("X-Correlation-Id", correlacao);
        var resp = await http.SendAsync(pedido);
        resp.StatusCode.Should().Be(HttpStatusCode.Created, await resp.Content.ReadAsStringAsync());
        var os = (await resp.Content.ReadFromJsonAsync<OrdemResponse>())!;

        // Comando entregue pelo outbox à fila do Billing (capturado pelo observador).
        var limite = DateTime.UtcNow.AddSeconds(10);
        while (!_fx.Observador.ComandosDoTipo<GerarOrcamento>(os.Id).Any() && DateTime.UtcNow < limite)
            await Task.Delay(100);
        var (recebido, _) = _fx.Observador.ComandosDoTipo<GerarOrcamento>(os.Id).Should().ContainSingle().Subject;
        recebido.CorrelationId.Should().Be(correlacao);

        // Evento interno consumido pela saga com o mesmo header.
        var consumido = _fx.Harness.Consumed.Select<OrdemServicoAberta>(c => c.Context.Message.OrdemServicoId == os.Id).Single();
        consumido.Context.Headers.Get<string>("X-Correlation-Id").Should().Be(correlacao);
    }
}
