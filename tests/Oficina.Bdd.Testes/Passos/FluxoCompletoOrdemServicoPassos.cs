using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Oficina.Aplicacao.Catalogo.Dtos;
using Oficina.Aplicacao.Estoque.Dtos;
using Oficina.Aplicacao.OrdensServico.Dtos;
using Oficina.Aplicacao.OrdensServico.Saga.Dtos;
using Oficina.Bdd.Testes.Suporte;
using Oficina.Infraestrutura.Mensageria.Contratos;
using Oficina.Infraestrutura.Persistencia;
using Reqnroll;

namespace Oficina.Bdd.Testes.Passos;

/// <summary>
/// Passos do fluxo completo da OS. O atendente usa a Api por HTTP; o Billing e a Execução são simulados:
/// os comandos que a saga envia a eles são lidos do <see cref="AmbienteBdd.Observador"/> e as respostas deles
/// são publicadas no bus. Cada publicação só retorna quando a saga já aplicou o evento (estado esperado
/// observado), para que dois eventos encadeados nunca sejam consumidos fora de ordem.
/// </summary>
[Binding]
public sealed class FluxoCompletoOrdemServicoPassos(ScenarioContext cenario)
{
    private static readonly TimeSpan LimiteComando = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan LimiteEstado = TimeSpan.FromSeconds(15);
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    private const string ChaveServicos = "servicos";
    private const string ChavePecas = "pecas";
    private const string ChaveOs = "os";
    private const string ChavePagamento = "pagamentoId";

    private static HttpClient Http => AmbienteBdd.ClienteAdmin;

    private Dictionary<string, Guid> Servicos => Ids(ChaveServicos);
    private Dictionary<string, Guid> Pecas => Ids(ChavePecas);
    private OrdemResponse Os => cenario.Get<OrdemResponse>(ChaveOs);

    // ------------------------------------------------------------------ Dado

    [Given(@"^o serviço ""(.*)"" de R\$ ([\d.,]+) e a peça ""(.*)"" de R\$ ([\d.,]+) com (\d+) unidades em estoque$")]
    public async Task DadoServicoEPecaComEstoque(string servico, string precoServico, string peca, string precoPeca, int saldo)
    {
        // Serviço e peça novos por cenário (SKU com Guid): os saldos de um cenário não interferem nos outros.
        var criado = await LerAsync<ServicoResponse>(await Http.PostAsJsonAsync("/api/v1/servicos",
            new CriarServicoRequest(servico, servico, Dinheiro(precoServico), 30)), HttpStatusCode.Created);
        Servicos[servico] = criado.Id;

        var novaPeca = await LerAsync<PecaResponse>(await Http.PostAsJsonAsync("/api/v1/pecas",
            new CriarPecaRequest($"BDD-{Guid.NewGuid():N}"[..16], peca, Dinheiro(precoPeca))), HttpStatusCode.Created);
        var entrada = await Http.PostAsJsonAsync($"/api/v1/pecas/{novaPeca.Id}/movimentacoes",
            new RegistrarMovimentacaoRequest("Entrada", saldo, "estoque inicial", null));
        entrada.IsSuccessStatusCode.Should().BeTrue(await entrada.Content.ReadAsStringAsync());
        Pecas[peca] = novaPeca.Id;
    }

    // ------------------------------------------------------------------ Atendente

    [When(@"^o atendente abre uma OS com (\d+) ""(.*)"" e (\d+) ""(.*)""$")]
    public async Task QuandoAtendenteAbreOs(int qtdServico, string servico, int qtdPeca, string peca)
    {
        var resposta = await Http.PostAsJsonAsync("/api/v1/ordens-servico", new AbrirOrdemRequest(
            new ClienteDadosDto("11144477735", "Cliente BDD", $"bdd{Guid.NewGuid():N}@x.com", "11987654321"),
            new VeiculoDadosDto($"BDD{Random.Shared.Next(1000, 9999)}", "Fiat", "Uno", 2020),
            new[] { new ItemServicoDto(Servicos[servico], qtdServico) },
            new[] { new ItemPecaDto(Pecas[peca], qtdPeca) }));

        var os = await LerAsync<OrdemResponse>(resposta, HttpStatusCode.Created);
        cenario.Set(os, ChaveOs);

        // A saga nasce do OrdemServicoAberta entregue pelo outbox: só depois dela o Billing pode responder.
        await AguardarSagaAsync("AguardandoOrcamento");
    }

    // ------------------------------------------------------------------ Billing e Execução publicam

    [When(@"^o Billing publica (OrcamentoGerado|OrcamentoAprovado|OrcamentoRejeitado|PagamentoConfirmado|PagamentoRecusado|PagamentoEstornado)$")]
    public async Task QuandoBillingPublica(string evento)
    {
        var id = Os.Id;
        var agora = DateTime.UtcNow;
        switch (evento)
        {
            case nameof(OrcamentoGerado):
                await PublicarEAguardarSagaAsync(new OrcamentoGerado
                {
                    OrdemServicoId = id, OcorridoEm = agora, OrcamentoId = Guid.NewGuid(), ValorTotal = Os.TotalGeral,
                }, "AguardandoAprovacao");
                break;
            case nameof(OrcamentoAprovado):
                await PublicarEAguardarSagaAsync(new OrcamentoAprovado
                {
                    OrdemServicoId = id, OcorridoEm = agora, OrcamentoId = Guid.NewGuid(), LinkPagamento = "https://mp.exemplo/p/bdd",
                }, "AguardandoPagamento");
                break;
            case nameof(OrcamentoRejeitado):
                await PublicarEAguardarSagaAsync(new OrcamentoRejeitado
                {
                    OrdemServicoId = id, OcorridoEm = agora, OrcamentoId = Guid.NewGuid(), Motivo = "Cliente achou caro",
                }, "Cancelada");
                break;
            case nameof(PagamentoConfirmado):
                var pagamentoId = $"pg-{id:N}";
                cenario.Set(pagamentoId, ChavePagamento);
                await PublicarEAguardarSagaAsync(new PagamentoConfirmado
                {
                    OrdemServicoId = id, OcorridoEm = agora, PagamentoId = pagamentoId, Valor = Os.TotalGeral, Meio = "pix",
                }, "AguardandoExecucao");
                break;
            case nameof(PagamentoRecusado):
                await PublicarEAguardarSagaAsync(new PagamentoRecusado
                {
                    OrdemServicoId = id, OcorridoEm = agora, Motivo = "cartao_recusado",
                }, "Cancelada");
                break;
            case nameof(PagamentoEstornado):
                await PublicarEAguardarSagaAsync(new PagamentoEstornado
                {
                    OrdemServicoId = id, OcorridoEm = agora, PagamentoId = cenario.Get<string>(ChavePagamento), Valor = Os.TotalGeral,
                }, "Cancelada");
                break;
        }
    }

    [When(@"^a Execução publica (ExecucaoEnfileirada|ExecucaoFinalizada|ExecucaoRecusada)$")]
    public async Task QuandoExecucaoPublica(string evento)
    {
        var id = Os.Id;
        var agora = DateTime.UtcNow;
        switch (evento)
        {
            case nameof(ExecucaoEnfileirada):
                await PublicarEAguardarSagaAsync(new ExecucaoEnfileirada { OrdemServicoId = id, OcorridoEm = agora, PosicaoFila = 1 }, "EmExecucao");
                break;
            case nameof(ExecucaoFinalizada):
                await PublicarEAguardarSagaAsync(new ExecucaoFinalizada
                {
                    OrdemServicoId = id, OcorridoEm = agora, Observacao = "Serviço concluído", DuracaoMinutos = 45,
                }, "Concluida");
                break;
            case nameof(ExecucaoRecusada):
                await PublicarEAguardarSagaAsync(new ExecucaoRecusada
                {
                    OrdemServicoId = id, OcorridoEm = agora, Motivo = "Sem mecânico habilitado",
                }, "Compensando");
                break;
        }
    }

    /// <summary>Status de execução não muda o estado da saga (segue EmExecucao): a prova do consumo é a OS.</summary>
    [When(@"^a Execução publica StatusExecucaoAlterado ""(EmDiagnostico|EmReparo)""$")]
    public async Task QuandoExecucaoPublicaStatus(string status)
    {
        await AmbienteBdd.Harness.Bus.Publish(new StatusExecucaoAlterado
        {
            OrdemServicoId = Os.Id, OcorridoEm = DateTime.UtcNow, StatusExecucao = Enum.Parse<StatusExecucao>(status),
        });
        await AguardarOsAsync(status);
    }

    // ------------------------------------------------------------------ Então

    [Then(@"^a OS fica ""(\w+)""$")]
    public Task EntaoOsFica(string status) => AguardarOsAsync(status);

    [Then(@"^a saga fica ""(\w+)""$")]
    public Task EntaoSagaFica(string estado) => AguardarSagaAsync(estado);

    [Then(@"^a peça ""(.*)"" fica com saldo (\d+) e (\d+) reservadas$")]
    public async Task EntaoPecaFica(string peca, int saldo, int reservadas)
    {
        PecaResponse? atual = null;
        await AguardarAsync(async () =>
        {
            atual = await Http.GetFromJsonAsync<PecaResponse>($"/api/v1/pecas/{Pecas[peca]}");
            return atual!.SaldoAtual == saldo && atual.SaldoReservado == reservadas;
        }, LimiteEstado, () => $"peça {peca} com saldo {saldo} e {reservadas} reservadas " +
                               $"(atual: saldo {atual?.SaldoAtual}, reservadas {atual?.SaldoReservado})");
    }

    [Then(@"^o Billing recebe o comando GerarOrcamento com (\d+) itens$")]
    public async Task EntaoBillingRecebeGerarOrcamento(int itens)
    {
        var comando = await AguardarComandoAsync<GerarOrcamento>();
        comando.NumeroOs.Should().Be(Os.Numero);
        comando.Itens.Should().HaveCount(itens);
        comando.Itens.Sum(i => i.PrecoUnitario * i.Quantidade).Should().Be(Os.TotalGeral);
    }

    [Then(@"^a Execução recebe o comando IniciarExecucao$")]
    public async Task EntaoExecucaoRecebeIniciarExecucao()
    {
        var comando = await AguardarComandoAsync<IniciarExecucao>();
        comando.NumeroOs.Should().Be(Os.Numero);
        comando.Servicos.Should().ContainSingle().Which.Quantidade.Should().Be(Os.ItensServico.Single().Quantidade);
        comando.Pecas.Should().ContainSingle().Which.Quantidade.Should().Be(Os.ItensPeca.Single().Quantidade);
    }

    [Then(@"^o Billing recebe o comando EstornarPagamento$")]
    public async Task EntaoBillingRecebeEstorno() =>
        (await AguardarComandoAsync<EstornarPagamento>()).Motivo.Should().NotBeNullOrWhiteSpace();

    /// <summary>
    /// Ausência só se afirma depois de uma barreira: o outbox drenado (tudo o que a saga gravou já foi enviado).
    /// A janela curta seguinte cobre a entrega in-memory do envio ao endpoint que faz o papel do Billing.
    /// </summary>
    [Then(@"^o Billing não recebe o comando (EstornarPagamento|CancelarOrcamento)$")]
    public async Task EntaoBillingNaoRecebe(string comando)
    {
        await AguardarAsync(async () => await ContarOutboxAsync() == 0, LimiteComando, () => "outbox da saga drenado");

        var fim = DateTime.UtcNow + TimeSpan.FromMilliseconds(500);
        do
        {
            ComandosRecebidos(comando).Should().Be(0, $"a saga não deve enviar {comando} ao Billing neste fluxo");
            await Task.Delay(100);
        } while (DateTime.UtcNow < fim);
    }

    // ------------------------------------------------------------------ apoio

    private int ComandosRecebidos(string comando) => comando switch
    {
        nameof(EstornarPagamento) => AmbienteBdd.Observador.ComandosDoTipo<EstornarPagamento>(Os.Id).Count(),
        nameof(CancelarOrcamento) => AmbienteBdd.Observador.ComandosDoTipo<CancelarOrcamento>(Os.Id).Count(),
        _ => throw new ArgumentOutOfRangeException(nameof(comando), comando, null),
    };

    private async Task PublicarEAguardarSagaAsync<T>(T evento, string estadoEsperado) where T : class
    {
        await AmbienteBdd.Harness.Bus.Publish(evento);
        await AguardarSagaAsync(estadoEsperado);
    }

    private async Task<T> AguardarComandoAsync<T>() where T : class, IMensagemDaSaga
    {
        await AguardarAsync(() => Task.FromResult(AmbienteBdd.Observador.ComandosDoTipo<T>(Os.Id).Any()),
            LimiteComando, () => $"{typeof(T).Name} recebido");
        return AmbienteBdd.Observador.ComandosDoTipo<T>(Os.Id).Should().ContainSingle().Subject.Mensagem;
    }

    private async Task AguardarOsAsync(string status)
    {
        string? atual = null;
        await AguardarAsync(async () =>
        {
            atual = (await Http.GetFromJsonAsync<OrdemResponse>($"/api/v1/ordens-servico/{Os.Id}"))!.Status;
            return atual == status;
        }, LimiteEstado, () => $"OS {status} (atual: {atual})");
    }

    private async Task AguardarSagaAsync(string estado)
    {
        string? atual = null;
        await AguardarAsync(async () =>
        {
            var resposta = await Http.GetAsync($"/api/v1/ordens-servico/{Os.Id}/saga");
            atual = resposta.StatusCode == HttpStatusCode.NotFound
                ? null
                : (await resposta.Content.ReadFromJsonAsync<EstadoSagaResponse>())!.Estado;
            return atual == estado;
        }, LimiteEstado, () => $"saga {estado} (atual: {atual ?? "sem saga"})");
    }

    private static async Task<int> ContarOutboxAsync()
    {
        using var escopo = AmbienteBdd.Factory.Services.CreateScope();
        return await escopo.ServiceProvider.GetRequiredService<OficinaDbContext>()
            .Set<MassTransit.EntityFrameworkCoreIntegration.OutboxMessage>().CountAsync();
    }

    private static async Task AguardarAsync(Func<Task<bool>> condicao, TimeSpan limite, Func<string> descricao)
    {
        var fim = DateTime.UtcNow + limite;
        while (DateTime.UtcNow < fim)
        {
            if (await condicao())
                return;
            await Task.Delay(100);
        }

        if (!await condicao())
            throw new TimeoutException($"Não aconteceu em {limite.TotalSeconds:0} s: {descricao()}.");
    }

    private static async Task<T> LerAsync<T>(HttpResponseMessage resposta, HttpStatusCode esperado)
    {
        resposta.StatusCode.Should().Be(esperado, await resposta.Content.ReadAsStringAsync());
        return (await resposta.Content.ReadFromJsonAsync<T>())!;
    }

    private static decimal Dinheiro(string texto) => decimal.Parse(texto, NumberStyles.Number, PtBr);

    private Dictionary<string, Guid> Ids(string chave)
    {
        if (!cenario.TryGetValue(chave, out Dictionary<string, Guid> ids))
            cenario.Set(ids = new Dictionary<string, Guid>(), chave);
        return ids;
    }
}
