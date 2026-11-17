using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using MassTransit;
using MassTransit.EntityFrameworkCoreIntegration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Oficina.Aplicacao.Catalogo.Dtos;
using Oficina.Aplicacao.Clientes.Dtos;
using Oficina.Aplicacao.Estoque.Dtos;
using Oficina.Aplicacao.OrdensServico.Dtos;
using Oficina.Aplicacao.OrdensServico.Saga;
using Oficina.Aplicacao.OrdensServico.Saga.Dtos;
using Oficina.Dominio.Estoque;
using Oficina.Dominio.OrdensServico;
using Oficina.Infraestrutura.Mensageria.Contratos;
using Oficina.Infraestrutura.Mensageria.Internos;
using Oficina.Infraestrutura.Mensageria.Saga;
using Oficina.Infraestrutura.Persistencia;
using Oficina.Integracao.Testes.Auth;

namespace Oficina.Integracao.Testes.Saga;

/// <summary>
/// Saga real (repositório EF, outbox e trilha no schema <c>saga</c> do PostgreSQL) sobre o bus in-memory do
/// harness. Billing e Execução não existem aqui: as respostas deles são publicadas pelo teste no harness.
/// </summary>
[Collection(nameof(AuthCollection))]
public class InicioDaSagaTestes
{
    private const int SaldoInicial = 10;
    private const int QuantidadePeca = 2;

    private readonly AuthFixture _fx;

    public InicioDaSagaTestes(AuthFixture fx) => _fx = fx;

    [Fact]
    public async Task AbrirOrdem_CriaSagaAguardandoOrcamento_EGravaPassoNaTrilha()
    {
        var (os, _) = await AbrirOrdemComPecaAsync();

        var saga = await AguardarSagaAsync(os.Id, "AguardandoOrcamento");

        saga.NumeroOs.Should().Be(os.Numero);
        var trilha = await TrilhaAsync(os.Id);
        trilha.Should().ContainSingle();
        trilha[0].Passo.Should().Be(PassosSaga.SolicitarOrcamento);
        trilha[0].Resultado.Should().Be(PassosSaga.Sucesso);
        trilha[0].Estado.Should().Be("AguardandoOrcamento");
        var (recebido, comando) = await AguardarComandoAsync<GerarOrcamento>(os.Id);
        recebido.Destino!.AbsolutePath.Should().EndWith("local-oficina-billing-comandos");
        recebido.FaultAddress.Should().BeNull();
        comando.NumeroOs.Should().Be(os.Numero);
        comando.Itens.Should().HaveCount(2);

        var estado = await ComEscopoAsync(sp => sp.GetRequiredService<ISagaOrdemServicoGateway>().ObterEstadoAsync(os.Id, default));
        estado!.Estado.Should().Be("AguardandoOrcamento");
        estado.NumeroOs.Should().Be(os.Numero);
        estado.Trilha.Should().ContainSingle().Which.Should().Match<PassoSagaResponse>(p =>
            p.Passo == PassosSaga.SolicitarOrcamento && p.Estado == "AguardandoOrcamento" && p.OcorridoEm.Offset == TimeSpan.Zero);
        (await ComEscopoAsync(sp => sp.GetRequiredService<ISagaOrdemServicoGateway>().ObterEstadoAsync(Guid.NewGuid(), default)))
            .Should().BeNull();
    }

    [Fact]
    public async Task AbrirOrdem_SaldoInsuficiente_NaoGravaMensagemNoOutbox()
    {
        var http = await AutenticadoAsync();
        var peca = await CriarPecaComSaldoAsync(http, 1);
        var servico = await CriarServicoAsync(http);
        await AguardarAsync(async () => await ContarOutboxAsync() == 0, TimeSpan.FromSeconds(10), "outbox drenado");
        var aberturasAntes = _fx.Observador.TotalDeTentativas<OrdemServicoAberta>();

        var resp = await http.PostAsJsonAsync("/api/v1/ordens-servico", NovaOrdem(servico.Id, peca.Id, quantidadePeca: 5));

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        await Task.Delay(1000); // > IntervaloOutboxMs (100 ms): uma mensagem gravada já teria sido entregue
        (await ContarOutboxAsync()).Should().Be(0);
        _fx.Observador.TotalDeTentativas<OrdemServicoAberta>().Should().Be(aberturasAntes);
    }

    [Fact]
    public async Task EventoAdiantado_ERetentadoSemPublicarFault()
    {
        var (os, _) = await AbrirOrdemComPecaAsync();
        await LevarSagaAteAguardandoExecucaoAsync(os.Id);
        var adiantado = new StatusExecucaoAlterado
        {
            OrdemServicoId = os.Id, OcorridoEm = DateTime.UtcNow, StatusExecucao = StatusExecucao.EmDiagnostico,
        };

        var idAdiantado = await PublicarAsync(adiantado);

        await AguardarAsync(() => Task.FromResult(_fx.Observador.Tentativas(idAdiantado) >= 4),
            TimeSpan.FromSeconds(10), "1 entrega + 3 retries");
        await Task.Delay(1000);
        _fx.Observador.Tentativas(idAdiantado).Should().Be(4, "depois dos retries vem o redelivery de 30 s");
        _fx.Harness.Published.Select<Fault<StatusExecucaoAlterado>>().Should().BeEmpty("PublishFaults = false");
        (await ObterOrdemAsync(os.Id)).Status.Should().Be(StatusOrdemDeServico.AguardandoPagamento);

        await _fx.Harness.Bus.Publish(new ExecucaoEnfileirada { OrdemServicoId = os.Id, OcorridoEm = DateTime.UtcNow, PosicaoFila = 1 });
        await AguardarSagaAsync(os.Id, "EmExecucao");

        // Redelivery (30 s) do evento adiantado, agora no estado certo.
        await AguardarAsync(async () => (await ObterOrdemAsync(os.Id)).Status == StatusOrdemDeServico.EmDiagnostico,
            TimeSpan.FromSeconds(60), "OS EmDiagnostico pelo redelivery");
        _fx.Harness.Published.Select<Fault<StatusExecucaoAlterado>>().Should().BeEmpty();
        (await TrilhaAsync(os.Id)).Where(p => p.Passo == PassosSaga.Execucao).Should().ContainSingle()
            .Which.Estado.Should().Be("EmExecucao");
    }

    [Fact]
    public async Task PagamentoConfirmado_ConflitoDeXminNaPeca_RetentaComContextoNovoEBaixaUmaVez()
    {
        var (os, pecaId) = await AbrirOrdemComPecaAsync();
        await LevarSagaAteAguardandoPagamentoAsync(os.Id);
        var conflitosAntes = _fx.InjetorDeFalhas.ConflitosInjetados;
        _fx.InjetorDeFalhas.ArmarConflitoNaProximaGravacaoDePeca();

        var idPagamento = await PublicarPagamentoConfirmadoAsync(os.Id);

        await AguardarSagaAsync(os.Id, "AguardandoExecucao", TimeSpan.FromSeconds(20));
        _fx.InjetorDeFalhas.ConflitosInjetados.Should().Be(conflitosAntes + 1);
        _fx.Observador.Tentativas(idPagamento).Should().Be(2, "a tentativa que perdeu o xmin falhou e o retry passou");
        await DeveTerBaixadoOEstoqueUmaVezAsync(os.Id, pecaId);
        await DeveTerEnviadoIniciarExecucaoUmaVezAsync(os.Id);
    }

    [Fact]
    public async Task PagamentoConfirmado_FalhaAoGravarOutbox_DesfazEstoqueESagaNaMesmaTransacao()
    {
        var (os, pecaId) = await AbrirOrdemComPecaAsync();
        await LevarSagaAteAguardandoPagamentoAsync(os.Id);
        _fx.InjetorDeFalhas.FalharOutboxDaSaga(os.Id);
        try
        {
            var idPagamento = await PublicarPagamentoConfirmadoAsync(os.Id);

            await AguardarAsync(() => Task.FromResult(_fx.Observador.Tentativas(idPagamento) >= 4),
                TimeSpan.FromSeconds(15), "1 entrega + 3 retries, todas falhando no outbox");
            await Task.Delay(500);
            _fx.InjetorDeFalhas.FalhasDeOutbox.Should().BeGreaterThanOrEqualTo(4);
        }
        finally
        {
            _fx.InjetorDeFalhas.FalharOutboxDaSaga(null);
        }

        // Cada tentativa baixou o estoque e avançou a saga antes de falhar no outbox: nada disso ficou.
        (await AguardarSagaAsync(os.Id, "AguardandoPagamento")).PagamentoId.Should().BeNull();
        var peca = await ObterPecaAsync(pecaId);
        peca.SaldoAtual.Should().Be(SaldoInicial);
        peca.SaldoReservado.Should().Be(QuantidadePeca);
        (await TrilhaAsync(os.Id)).Should().NotContain(p => p.Passo == PassosSaga.ConfirmarPagamento);
        _fx.Observador.ComandosDoTipo<IniciarExecucao>(os.Id).Should().BeEmpty();

        // Redelivery (30 s) sem a falha: aplica tudo uma única vez.
        await AguardarSagaAsync(os.Id, "AguardandoExecucao", TimeSpan.FromSeconds(60));
        await DeveTerBaixadoOEstoqueUmaVezAsync(os.Id, pecaId);
        await DeveTerEnviadoIniciarExecucaoUmaVezAsync(os.Id);
        (await TrilhaAsync(os.Id)).Where(p => p.Passo == PassosSaga.ConfirmarPagamento).Should().ContainSingle()
            .Which.Estado.Should().Be("AguardandoExecucao");
    }

    // ---------------------------------------------------------------- helpers

    private async Task DeveTerBaixadoOEstoqueUmaVezAsync(Guid osId, Guid pecaId)
    {
        var peca = await ObterPecaAsync(pecaId);
        peca.SaldoAtual.Should().Be(SaldoInicial - QuantidadePeca);
        peca.SaldoReservado.Should().Be(0);
        peca.Movimentacoes.Where(m => m.OrdemServicoId == osId && m.Tipo == TipoMovimentacao.Saida)
            .Should().ContainSingle().Which.Quantidade.Should().Be(QuantidadePeca);
    }

    /// <summary>
    /// O <c>IniciarExecucao</c> é montado do resultado do passo guardado no contexto do consumo (não persistido):
    /// prova que esse resultado chega ao Send sob o outbox EF.
    /// </summary>
    private async Task DeveTerEnviadoIniciarExecucaoUmaVezAsync(Guid osId)
    {
        await AguardarComandoAsync<IniciarExecucao>(osId);
        await Task.Delay(500);
        var (recebido, comando) = _fx.Observador.ComandosDoTipo<IniciarExecucao>(osId).Should().ContainSingle().Subject;
        recebido.Destino!.AbsolutePath.Should().EndWith("local-oficina-execucao-comandos");
        recebido.FaultAddress.Should().BeNull();
        comando.Pecas.Should().ContainSingle().Which.Quantidade.Should().Be(QuantidadePeca);
        comando.Servicos.Should().ContainSingle();
    }

    private async Task LevarSagaAteAguardandoPagamentoAsync(Guid osId)
    {
        await AguardarSagaAsync(osId, "AguardandoOrcamento");
        await _fx.Harness.Bus.Publish(new OrcamentoGerado
        {
            OrdemServicoId = osId, OcorridoEm = DateTime.UtcNow, OrcamentoId = Guid.NewGuid(), ValorTotal = 200m,
        });
        await AguardarSagaAsync(osId, "AguardandoAprovacao");
        await _fx.Harness.Bus.Publish(new OrcamentoAprovado
        {
            OrdemServicoId = osId, OcorridoEm = DateTime.UtcNow, OrcamentoId = Guid.NewGuid(), LinkPagamento = "https://mp/p/1",
        });
        await AguardarSagaAsync(osId, "AguardandoPagamento");
    }

    private async Task LevarSagaAteAguardandoExecucaoAsync(Guid osId)
    {
        await LevarSagaAteAguardandoPagamentoAsync(osId);
        await PublicarPagamentoConfirmadoAsync(osId);
        await AguardarSagaAsync(osId, "AguardandoExecucao");
    }

    /// <summary>Publica com MessageId conhecido, para contar as tentativas de consumo.</summary>
    private async Task<Guid> PublicarAsync<T>(T mensagem) where T : class
    {
        var id = NewId.NextGuid();
        await _fx.Harness.Bus.Publish(mensagem, c => c.MessageId = id);
        return id;
    }

    private Task<Guid> PublicarPagamentoConfirmadoAsync(Guid osId) =>
        PublicarAsync(new PagamentoConfirmado
        {
            OrdemServicoId = osId, OcorridoEm = DateTime.UtcNow, PagamentoId = "pg-" + osId.ToString("N"), Valor = 200m, Meio = "pix",
        });

    private async Task<(ObservadorDaSaga.ComandoRecebido Recebido, T Mensagem)> AguardarComandoAsync<T>(Guid osId)
        where T : class, IMensagemDaSaga
    {
        await AguardarAsync(() => Task.FromResult(_fx.Observador.ComandosDoTipo<T>(osId).Any()),
            TimeSpan.FromSeconds(10), $"{typeof(T).Name} recebido");
        return _fx.Observador.ComandosDoTipo<T>(osId).First();
    }

    private async Task<EstadoSagaOrdemServico> AguardarSagaAsync(Guid osId, string estado, TimeSpan? limite = null)
    {
        EstadoSagaOrdemServico? saga = null;
        await AguardarAsync(async () =>
        {
            saga = await ComBancoAsync(db => db.Set<EstadoSagaOrdemServico>().AsNoTracking()
                .SingleOrDefaultAsync(s => s.CorrelationId == osId));
            return saga?.EstadoAtual == estado;
        }, limite ?? TimeSpan.FromSeconds(10), $"saga em {estado} (atual: {saga?.EstadoAtual ?? "sem saga"})");
        return saga!;
    }

    private Task<List<PassoSaga>> TrilhaAsync(Guid osId) =>
        ComBancoAsync(db => db.Set<PassoSaga>().AsNoTracking()
            .Where(p => p.OrdemServicoId == osId).OrderBy(p => p.OcorridoEm).ToListAsync());

    private Task<int> ContarOutboxAsync() => ComBancoAsync(db => db.Set<OutboxMessage>().CountAsync());

    private Task<OrdemDeServico> ObterOrdemAsync(Guid osId) =>
        ComBancoAsync(db => db.OrdensServico.AsNoTracking().SingleAsync(o => o.Id == osId));

    private Task<Peca> ObterPecaAsync(Guid pecaId) =>
        ComBancoAsync(db => db.Pecas.AsNoTracking().Include(p => p.Movimentacoes).SingleAsync(p => p.Id == pecaId));

    private Task<T> ComBancoAsync<T>(Func<OficinaDbContext, Task<T>> consulta) =>
        ComEscopoAsync(sp => consulta(sp.GetRequiredService<OficinaDbContext>()));

    private async Task<T> ComEscopoAsync<T>(Func<IServiceProvider, Task<T>> acao)
    {
        using var scope = _fx.Factory.Services.CreateScope();
        return await acao(scope.ServiceProvider);
    }

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
                new CriarPecaRequest($"SAGA-{Guid.NewGuid():N}"[..12], "Filtro de óleo", 25m)))
            .Content.ReadFromJsonAsync<PecaResponse>())!;
        (await http.PostAsJsonAsync($"/api/v1/pecas/{peca.Id}/movimentacoes",
            new RegistrarMovimentacaoRequest("Entrada", saldo, "compra", null))).EnsureSuccessStatusCode();
        return peca;
    }

    private static AbrirOrdemRequest NovaOrdem(Guid servicoId, Guid pecaId, int quantidadePeca) =>
        new(
            new ClienteDadosDto("11144477735", "Cliente Saga", $"s{Guid.NewGuid():N}@x.com", "11987654321"),
            new VeiculoDadosDto($"SGA{Random.Shared.Next(1000, 9999)}", "Fiat", "Uno", 2020),
            new[] { new ItemServicoDto(servicoId, 1) },
            new[] { new ItemPecaDto(pecaId, quantidadePeca) });

    private async Task<(OrdemResponse Os, Guid PecaId)> AbrirOrdemComPecaAsync()
    {
        var http = await AutenticadoAsync();
        var servico = await CriarServicoAsync(http);
        var peca = await CriarPecaComSaldoAsync(http, SaldoInicial);

        var resp = await http.PostAsJsonAsync("/api/v1/ordens-servico", NovaOrdem(servico.Id, peca.Id, QuantidadePeca));

        resp.StatusCode.Should().Be(HttpStatusCode.Created, await resp.Content.ReadAsStringAsync());
        return ((await resp.Content.ReadFromJsonAsync<OrdemResponse>())!, peca.Id);
    }
}
