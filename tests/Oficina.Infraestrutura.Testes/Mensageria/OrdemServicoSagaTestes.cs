using FluentAssertions;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using Oficina.Aplicacao.OrdensServico.Saga;
using Oficina.Aplicacao.OrdensServico.Saga.Dtos;
using Oficina.Infraestrutura.Mensageria;
using Oficina.Infraestrutura.Mensageria.Contratos;
using Oficina.Infraestrutura.Mensageria.Internos;
using Oficina.Infraestrutura.Mensageria.Saga;

namespace Oficina.Infraestrutura.Testes.Mensageria;

public sealed class OrdemServicoSagaTestes : IAsyncLifetime
{
    private const string PagamentoId = "1325123456";

    private static readonly string[] TodosOsEventos =
    {
        "OrdemServicoAberta", "OrcamentoGerado", "OrcamentoFalhou", "OrcamentoAprovado", "OrcamentoRejeitado",
        "PagamentoConfirmado", "PagamentoRecusado", "ExecucaoEnfileirada", "ExecucaoRecusada",
        "StatusExecucaoAlterado", "ExecucaoFinalizada", "PagamentoEstornado", "EstornoFalhou",
        "CancelamentoSolicitado",
    };

    /// <summary>Eventos tratados (com efeito ou registro) em cada estado — escrito à mão, independente da saga.</summary>
    private static readonly Dictionary<string, string[]> EventosTratadosPorEstado = new()
    {
        ["AguardandoOrcamento"] = new[] { "OrcamentoGerado", "OrcamentoFalhou", "OrcamentoAprovado", "OrcamentoRejeitado", "CancelamentoSolicitado" },
        ["AguardandoAprovacao"] = new[] { "OrcamentoAprovado", "OrcamentoRejeitado", "CancelamentoSolicitado" },
        ["AguardandoPagamento"] = new[] { "PagamentoConfirmado", "PagamentoRecusado", "CancelamentoSolicitado" },
        ["AguardandoExecucao"] = new[] { "ExecucaoEnfileirada", "ExecucaoRecusada", "CancelamentoSolicitado" },
        ["EmExecucao"] = new[] { "StatusExecucaoAlterado", "ExecucaoFinalizada", "CancelamentoSolicitado" },
        ["Compensando"] = new[] { "PagamentoConfirmado", "PagamentoEstornado", "EstornoFalhou", "CancelamentoSolicitado" },
        ["Concluida"] = Array.Empty<string>(),
        ["Cancelada"] = new[] { "PagamentoConfirmado" },
    };

    /// <summary>Eventos de etapa futura: não declarados → falham → retry/redelivery até a saga chegar lá.</summary>
    private static readonly Dictionary<string, string[]> EventosAdiantadosPorEstado = new()
    {
        ["AguardandoOrcamento"] = new[] { "PagamentoConfirmado", "PagamentoRecusado", "ExecucaoEnfileirada", "ExecucaoRecusada", "StatusExecucaoAlterado", "ExecucaoFinalizada", "PagamentoEstornado", "EstornoFalhou" },
        ["AguardandoAprovacao"] = new[] { "PagamentoConfirmado", "PagamentoRecusado", "ExecucaoEnfileirada", "ExecucaoRecusada", "StatusExecucaoAlterado", "ExecucaoFinalizada", "PagamentoEstornado", "EstornoFalhou" },
        ["AguardandoPagamento"] = new[] { "ExecucaoEnfileirada", "ExecucaoRecusada", "StatusExecucaoAlterado", "ExecucaoFinalizada", "PagamentoEstornado", "EstornoFalhou" },
        ["AguardandoExecucao"] = new[] { "StatusExecucaoAlterado", "ExecucaoFinalizada", "PagamentoEstornado", "EstornoFalhou" },
        ["EmExecucao"] = new[] { "PagamentoEstornado", "EstornoFalhou" },
        ["Compensando"] = Array.Empty<string>(),
        ["Concluida"] = Array.Empty<string>(),
        ["Cancelada"] = Array.Empty<string>(),
    };

    public static TheoryData<string, string> ParesIgnorados()
    {
        var dados = new TheoryData<string, string>();
        foreach (var (estado, eventos) in OrdemServicoSaga.EventosIgnoradosPorEstado)
            foreach (var evento in eventos)
                dados.Add(estado, evento);
        return dados;
    }

    public static TheoryData<string, string> ParesAdiantados()
    {
        var dados = new TheoryData<string, string>();
        foreach (var (estado, eventos) in EventosAdiantadosPorEstado)
            foreach (var evento in eventos)
                dados.Add(estado, evento);
        return dados;
    }

    private readonly Guid _id = Guid.NewGuid();
    private readonly Mock<IPassosSagaOrdemServico> _passos = new();
    private readonly RegistradorPassoSagaFake _registrador = new();
    private ServiceProvider _provider = null!;
    private ITestHarness _harness = null!;
    private ISagaStateMachineTestHarness<OrdemServicoSaga, EstadoSagaOrdemServico> _sagaHarness = null!;

    private static readonly PedidoOrcamento Pedido = new(
        4321, "12345678909", "Maria Souza", "maria@exemplo.com",
        new List<ItemPedidoOrcamento>
        {
            new(TipoItemPedido.Servico, Guid.NewGuid(), "Troca de óleo", 1, 120m),
            new(TipoItemPedido.Peca, Guid.NewGuid(), "Filtro de óleo", 2, 35.5m),
        });

    private static readonly PedidoExecucao PedidoExecucao = new(
        4321, "Centro",
        new List<ItemPedidoExecucao> { new("Troca de óleo", 1) },
        new List<ItemPedidoExecucao> { new("Filtro de óleo", 2) });

    public async Task InitializeAsync()
    {
        _passos.Setup(p => p.MontarPedidoOrcamentoAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Pedido);
        _passos.Setup(p => p.ConfirmarPagamentoAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PedidoExecucao);
        _passos.Setup(p => p.RegistrarStatusExecucaoAsync(It.IsAny<Guid>(), It.IsAny<EtapaExecucao>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _provider = new ServiceCollection()
            .AddSingleton(Options.Create(new OpcoesMensageria { PrefixoAmbiente = "teste" }))
            .AddScoped(_ => _passos.Object)
            .AddSingleton<IRegistradorPassoSaga>(_registrador)
            .AddMassTransitTestHarness(x =>
            {
                // Inatividade curta: os helpers fazem polling próprio; Any/Select do harness só leem o que já chegou.
                x.SetTestTimeouts(testTimeout: TimeSpan.FromSeconds(10), testInactivityTimeout: TimeSpan.FromMilliseconds(50));
                x.AddSagaStateMachine<OrdemServicoSaga, EstadoSagaOrdemServico>().InMemoryRepository();
            })
            .BuildServiceProvider(true);

        _harness = _provider.GetRequiredService<ITestHarness>();
        await _harness.Start();
        _sagaHarness = _harness.GetSagaStateMachineHarness<OrdemServicoSaga, EstadoSagaOrdemServico>();
    }

    public async Task DisposeAsync()
    {
        await _harness.Stop();
        await _provider.DisposeAsync();
    }

    // ---------------------------------------------------------------- transições

    [Fact]
    public async Task AberturaDaOs_EnviaGerarOrcamentoParaFilaDoBilling_EFicaAguardandoOrcamento()
    {
        await PublicarSemErroAsync(Mensagem("OrdemServicoAberta"));

        await DeveEstarEmAsync("AguardandoOrcamento");
        (await _harness.Sent.Any<GerarOrcamento>()).Should().BeTrue();
        var enviado = _harness.Sent.Select<GerarOrcamento>().Single();
        enviado.Context.DestinationAddress!.AbsolutePath.Should().EndWith("teste-oficina-billing-comandos");
        var comando = enviado.Context.Message;
        comando.OrdemServicoId.Should().Be(_id);
        comando.NumeroOs.Should().Be(4321);
        comando.ClienteDocumento.Should().Be("12345678909");
        comando.ClienteNome.Should().Be("Maria Souza");
        comando.ClienteEmail.Should().Be("maria@exemplo.com");
        comando.OcorridoEm.Kind.Should().Be(DateTimeKind.Utc);
        comando.Itens.Should().HaveCount(2);
        comando.Itens[0].Should().BeEquivalentTo(new ItemOrcamento
        {
            Tipo = TipoItemOrcamento.Servico, ReferenciaId = Pedido.Itens[0].ReferenciaId,
            Descricao = "Troca de óleo", Quantidade = 1, PrecoUnitario = 120m,
        });
        comando.Itens[1].Tipo.Should().Be(TipoItemOrcamento.Peca);
        comando.Itens[1].PrecoUnitario.Should().Be(35.5m);

        var saga = Saga();
        saga.NumeroOs.Should().Be(4321);
        saga.CriadaEm.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        saga.AtualizadaEm.Should().Be(saga.CriadaEm);
        _registrador.Passos.Should().Equal((PassosSaga.SolicitarOrcamento, PassosSaga.Sucesso, false));
    }

    [Fact]
    public async Task OrcamentoGerado_AvancaParaAguardandoAprovacao()
    {
        await LevarSagaAteAsync("AguardandoOrcamento");

        await PublicarSemErroAsync(Mensagem("OrcamentoGerado"));

        await DeveEstarEmAsync("AguardandoAprovacao");
        _passos.Verify(p => p.RegistrarOrcamentoGeradoAsync(_id, It.IsAny<CancellationToken>()), Times.Once);
        _registrador.Passos.Should().Equal((PassosSaga.GerarOrcamento, PassosSaga.Sucesso, false));
    }

    [Fact]
    public async Task OrcamentoFalhou_CancelaOsComMotivo()
    {
        await LevarSagaAteAsync("AguardandoOrcamento");

        await PublicarSemErroAsync(new OrcamentoFalhou { OrdemServicoId = _id, OcorridoEm = DateTime.UtcNow, Motivo = "Orcamento sem itens" });

        await DeveEstarEmAsync("Cancelada");
        _passos.Verify(p => p.CancelarAsync(_id, "Orcamento sem itens", It.IsAny<CancellationToken>()), Times.Once);
        Saga().Motivo.Should().Be("Orcamento sem itens");
        _registrador.Passos.Should().Equal((PassosSaga.GerarOrcamento, PassosSaga.Falha, true));
    }

    [Fact]
    public async Task OrcamentoAprovado_AvancaParaAguardandoPagamento()
    {
        await LevarSagaAteAsync("AguardandoAprovacao");

        await PublicarSemErroAsync(Mensagem("OrcamentoAprovado"));

        await DeveEstarEmAsync("AguardandoPagamento");
        _passos.Verify(p => p.RegistrarOrcamentoAprovadoAsync(_id, It.IsAny<CancellationToken>()), Times.Once);
        _passos.VerifyNoOtherCalls();
        _registrador.Passos.Should().Equal((PassosSaga.AprovarOrcamento, PassosSaga.Sucesso, false));
    }

    [Fact]
    public async Task OrcamentoRejeitado_CancelaOs()
    {
        await LevarSagaAteAsync("AguardandoAprovacao");

        await PublicarSemErroAsync(new OrcamentoRejeitado
        {
            OrdemServicoId = _id, OcorridoEm = DateTime.UtcNow, OrcamentoId = Guid.NewGuid(), Motivo = "Cliente achou caro",
        });

        await DeveEstarEmAsync("Cancelada");
        _passos.Verify(p => p.RegistrarOrcamentoRejeitadoAsync(_id, "Cliente achou caro", It.IsAny<CancellationToken>()), Times.Once);
        _passos.VerifyNoOtherCalls();
        Saga().Motivo.Should().Be("Orçamento rejeitado: Cliente achou caro");
        _registrador.Passos.Should().Equal((PassosSaga.AprovarOrcamento, PassosSaga.Falha, true));
    }

    [Fact]
    public async Task OrcamentoRejeitado_SemMotivo_UsaMotivoPadraoNaSaga()
    {
        await LevarSagaAteAsync("AguardandoAprovacao");

        await PublicarSemErroAsync(new OrcamentoRejeitado { OrdemServicoId = _id, OcorridoEm = DateTime.UtcNow, OrcamentoId = Guid.NewGuid() });

        await DeveEstarEmAsync("Cancelada");
        Saga().Motivo.Should().Be("Orçamento rejeitado: sem motivo");
        _passos.Verify(p => p.RegistrarOrcamentoRejeitadoAsync(_id, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    // Billing pode cair entre salvar o orçamento e publicar OrcamentoGerado: o GerarOrcamento
    // reentregue não publica nada, e a próxima notícia da saga é a decisão do cliente.
    [Fact]
    public async Task OrcamentoAprovado_SemOrcamentoGerado_RegistraGeradoEAprovadoEAvancaParaAguardandoPagamento()
    {
        await LevarSagaAteAsync("AguardandoOrcamento");
        var ordem = new List<string>();
        _passos.Setup(p => p.RegistrarOrcamentoGeradoAsync(_id, It.IsAny<CancellationToken>()))
            .Callback(() => ordem.Add("Gerado")).Returns(Task.CompletedTask);
        _passos.Setup(p => p.RegistrarOrcamentoAprovadoAsync(_id, It.IsAny<CancellationToken>()))
            .Callback(() => ordem.Add("Aprovado")).Returns(Task.CompletedTask);

        await PublicarSemErroAsync(Mensagem("OrcamentoAprovado"));

        await DeveEstarEmAsync("AguardandoPagamento");
        ordem.Should().Equal("Gerado", "Aprovado");
        _passos.Verify(p => p.RegistrarOrcamentoGeradoAsync(_id, It.IsAny<CancellationToken>()), Times.Once);
        _passos.Verify(p => p.RegistrarOrcamentoAprovadoAsync(_id, It.IsAny<CancellationToken>()), Times.Once);
        _passos.VerifyNoOtherCalls();
        _registrador.Passos.Should().Equal(
            (PassosSaga.GerarOrcamento, PassosSaga.Sucesso, false),
            (PassosSaga.AprovarOrcamento, PassosSaga.Sucesso, false));
    }

    [Fact]
    public async Task OrcamentoRejeitado_SemOrcamentoGerado_RegistraGeradoERejeitaECancela()
    {
        await LevarSagaAteAsync("AguardandoOrcamento");
        var ordem = new List<string>();
        _passos.Setup(p => p.RegistrarOrcamentoGeradoAsync(_id, It.IsAny<CancellationToken>()))
            .Callback(() => ordem.Add("Gerado")).Returns(Task.CompletedTask);
        _passos.Setup(p => p.RegistrarOrcamentoRejeitadoAsync(_id, "Cliente achou caro", It.IsAny<CancellationToken>()))
            .Callback(() => ordem.Add("Rejeitado")).Returns(Task.CompletedTask);

        await PublicarSemErroAsync(new OrcamentoRejeitado
        {
            OrdemServicoId = _id, OcorridoEm = DateTime.UtcNow, OrcamentoId = Guid.NewGuid(), Motivo = "Cliente achou caro",
        });

        await DeveEstarEmAsync("Cancelada");
        ordem.Should().Equal("Gerado", "Rejeitado");
        _passos.Verify(p => p.RegistrarOrcamentoGeradoAsync(_id, It.IsAny<CancellationToken>()), Times.Once);
        _passos.Verify(p => p.RegistrarOrcamentoRejeitadoAsync(_id, "Cliente achou caro", It.IsAny<CancellationToken>()), Times.Once);
        _passos.VerifyNoOtherCalls();
        Saga().Motivo.Should().Be("Orçamento rejeitado: Cliente achou caro");
        _registrador.Passos.Should().Equal(
            (PassosSaga.GerarOrcamento, PassosSaga.Sucesso, false),
            (PassosSaga.AprovarOrcamento, PassosSaga.Falha, true));
    }

    [Fact]
    public async Task PagamentoConfirmado_BaixaEstoqueEEnviaIniciarExecucao()
    {
        await LevarSagaAteAsync("AguardandoPagamento");

        await PublicarSemErroAsync(Mensagem("PagamentoConfirmado"));

        await DeveEstarEmAsync("AguardandoExecucao");
        _passos.Verify(p => p.ConfirmarPagamentoAsync(_id, It.IsAny<CancellationToken>()), Times.Once);
        (await _harness.Sent.Any<IniciarExecucao>()).Should().BeTrue();
        var enviado = _harness.Sent.Select<IniciarExecucao>().Single();
        enviado.Context.DestinationAddress!.AbsolutePath.Should().EndWith("teste-oficina-execucao-comandos");
        var comando = enviado.Context.Message;
        comando.OrdemServicoId.Should().Be(_id);
        comando.NumeroOs.Should().Be(4321);
        comando.Unidade.Should().Be("Centro");
        comando.Servicos.Should().BeEquivalentTo(new[] { new ItemExecucao { Descricao = "Troca de óleo", Quantidade = 1 } });
        comando.Pecas.Should().BeEquivalentTo(new[] { new ItemExecucao { Descricao = "Filtro de óleo", Quantidade = 2 } });
        Saga().PagamentoId.Should().Be(PagamentoId);
        _harness.Sent.Select<EstornarPagamento>().Should().BeEmpty();
        _registrador.Passos.Should().Equal((PassosSaga.ConfirmarPagamento, PassosSaga.Sucesso, false));
    }

    [Fact]
    public async Task PagamentoConfirmado_PagamentoNaoPodeSerHonrado_EnviaEstornoEFicaCompensando()
    {
        await LevarSagaAteAsync("AguardandoPagamento");
        _passos.Setup(p => p.ConfirmarPagamentoAsync(_id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PedidoExecucao?)null);

        await PublicarSemErroAsync(Mensagem("PagamentoConfirmado"));

        await DeveEstarEmAsync("Compensando");
        (await _harness.Sent.Any<EstornarPagamento>()).Should().BeTrue();
        var estorno = _harness.Sent.Select<EstornarPagamento>().Single();
        estorno.Context.DestinationAddress!.AbsolutePath.Should().EndWith("teste-oficina-billing-comandos");
        estorno.Context.Message.Motivo.Should().NotBeNullOrWhiteSpace();
        _harness.Sent.Select<IniciarExecucao>().Should().BeEmpty();
        Saga().PagamentoId.Should().Be(PagamentoId);
        Saga().Motivo.Should().Be(estorno.Context.Message.Motivo);
        _registrador.Passos.Should().Equal((PassosSaga.ConfirmarPagamento, PassosSaga.Falha, true));
    }

    [Fact]
    public async Task PagamentoRecusado_CancelaSemEstornoNemCancelarOrcamento()
    {
        await LevarSagaAteAsync("AguardandoPagamento");

        await PublicarSemErroAsync(new PagamentoRecusado { OrdemServicoId = _id, OcorridoEm = DateTime.UtcNow, Motivo = "cartao_recusado" });

        await DeveEstarEmAsync("Cancelada");
        _passos.Verify(p => p.CancelarAsync(_id, "cartao_recusado", It.IsAny<CancellationToken>()), Times.Once);
        _passos.VerifyNoOtherCalls();
        _harness.Sent.Select<EstornarPagamento>().Should().BeEmpty();
        _harness.Sent.Select<CancelarOrcamento>().Should().BeEmpty();
        _registrador.Passos.Should().Equal((PassosSaga.ConfirmarPagamento, PassosSaga.Falha, true));
    }

    [Theory]
    [InlineData("valor_divergente")]
    [InlineData("pagamento_estornado")]
    [InlineData("pagamento_expirado")]
    public async Task PagamentoRecusado_QualquerMotivoDoBilling_EFalhaDePagamento(string motivo)
    {
        await LevarSagaAteAsync("AguardandoPagamento");

        await PublicarSemErroAsync(new PagamentoRecusado { OrdemServicoId = _id, OcorridoEm = DateTime.UtcNow, PagamentoId = PagamentoId, Motivo = motivo });

        await DeveEstarEmAsync("Cancelada");
        Saga().Motivo.Should().Be(motivo);
        _passos.Verify(p => p.CancelarAsync(_id, motivo, It.IsAny<CancellationToken>()), Times.Once);
        _passos.Verify(p => p.ConfirmarPagamentoAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _harness.Sent.Select<EstornarPagamento>().Should().BeEmpty();
    }

    [Fact]
    public async Task ExecucaoEnfileirada_FicaEmExecucao()
    {
        await LevarSagaAteAsync("AguardandoExecucao");

        await PublicarSemErroAsync(Mensagem("ExecucaoEnfileirada"));

        await DeveEstarEmAsync("EmExecucao");
        _passos.Verify(p => p.RegistrarExecucaoEnfileiradaAsync(_id, It.IsAny<CancellationToken>()), Times.Once);
        _registrador.Passos.Should().Equal((PassosSaga.EnfileirarExecucao, PassosSaga.Sucesso, false));
    }

    [Theory]
    [InlineData(StatusExecucao.EmDiagnostico)]
    [InlineData(StatusExecucao.EmReparo)]
    public async Task StatusExecucaoAlterado_RegistraEtapaSemMudarEstado(StatusExecucao status)
    {
        await LevarSagaAteAsync("EmExecucao");

        await PublicarSemErroAsync(new StatusExecucaoAlterado { OrdemServicoId = _id, OcorridoEm = DateTime.UtcNow, StatusExecucao = status });

        await DeveEstarEmAsync("EmExecucao");
        var etapa = Enum.Parse<EtapaExecucao>(status.ToString());
        _passos.Verify(p => p.RegistrarStatusExecucaoAsync(_id, etapa, It.IsAny<CancellationToken>()), Times.Once);
        _registrador.Registros.Should().Equal((PassosSaga.Execucao, PassosSaga.Sucesso, false, status.ToString()));
    }

    [Fact]
    public async Task StatusExecucaoAlterado_StatusJaAplicadoNaOs_NaoGravaPassoNaTrilha()
    {
        await LevarSagaAteAsync("EmExecucao");
        _passos.Setup(p => p.RegistrarStatusExecucaoAsync(_id, EtapaExecucao.EmDiagnostico, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        await PublicarSemErroAsync(new StatusExecucaoAlterado { OrdemServicoId = _id, OcorridoEm = DateTime.UtcNow, StatusExecucao = StatusExecucao.EmDiagnostico });

        await DeveEstarEmAsync("EmExecucao");
        _passos.Verify(p => p.RegistrarStatusExecucaoAsync(_id, EtapaExecucao.EmDiagnostico, It.IsAny<CancellationToken>()), Times.Once);
        _registrador.Registros.Should().BeEmpty("o status repetido não muda a OS e não é um passo novo");
    }

    [Fact]
    public async Task Trilha_CaminhoFeliz_RegistraOEstadoNovoDeCadaPasso()
    {
        await LevarSagaAteAsync("EmExecucao", limparRegistros: false);
        await PublicarSemErroAsync(Mensagem("StatusExecucaoAlterado"));
        await PublicarSemErroAsync(Mensagem("ExecucaoFinalizada"));

        _registrador.Estados.Should().Equal(
            "AguardandoOrcamento", "AguardandoAprovacao", "AguardandoPagamento", "AguardandoExecucao",
            "EmExecucao", "EmExecucao", "Concluida");
    }

    [Fact]
    public async Task Trilha_Compensacao_RegistraOEstadoNovoDeCadaPasso()
    {
        await LevarSagaAteAsync("Compensando", limparRegistros: false);
        await PublicarSemErroAsync(Mensagem("PagamentoEstornado"));

        _registrador.Passos.Select(p => p.Passo).Should().Equal(
            PassosSaga.SolicitarOrcamento, PassosSaga.GerarOrcamento, PassosSaga.AprovarOrcamento,
            PassosSaga.ConfirmarPagamento, PassosSaga.EnfileirarExecucao, PassosSaga.EstornarPagamento);
        _registrador.Estados.Should().Equal(
            "AguardandoOrcamento", "AguardandoAprovacao", "AguardandoPagamento", "AguardandoExecucao",
            "Compensando", "Cancelada");
    }

    [Fact]
    public async Task Trilha_OrcamentoGeradoImplicito_RegistraAguardandoAprovacaoAntesDeAguardandoPagamento()
    {
        await LevarSagaAteAsync("AguardandoOrcamento");

        await PublicarSemErroAsync(Mensagem("OrcamentoAprovado"));

        _registrador.Estados.Should().Equal("AguardandoAprovacao", "AguardandoPagamento");
    }

    [Fact]
    public async Task ExecucaoFinalizada_ConcluiSaga()
    {
        await LevarSagaAteAsync("EmExecucao");

        await PublicarSemErroAsync(Mensagem("ExecucaoFinalizada"));

        await DeveEstarEmAsync("Concluida");
        _passos.Verify(p => p.RegistrarExecucaoFinalizadaAsync(_id, It.IsAny<CancellationToken>()), Times.Once);
        _registrador.Passos.Should().Equal((PassosSaga.FinalizarExecucao, PassosSaga.Sucesso, false));
    }

    [Fact]
    public async Task ExecucaoRecusada_EnviaEstornarPagamentoEFicaCompensando()
    {
        await LevarSagaAteAsync("AguardandoExecucao");

        await PublicarSemErroAsync(new ExecucaoRecusada { OrdemServicoId = _id, OcorridoEm = DateTime.UtcNow, Motivo = "Oficina sem capacidade" });

        await DeveEstarEmAsync("Compensando");
        (await _harness.Sent.Any<EstornarPagamento>()).Should().BeTrue();
        var estorno = _harness.Sent.Select<EstornarPagamento>().Single();
        estorno.Context.DestinationAddress!.AbsolutePath.Should().EndWith("teste-oficina-billing-comandos");
        estorno.Context.Message.Motivo.Should().Be("Oficina sem capacidade");
        estorno.Context.Message.OrdemServicoId.Should().Be(_id);
        Saga().Motivo.Should().Be("Oficina sem capacidade");
        _passos.VerifyNoOtherCalls();
        _registrador.Passos.Should().Equal((PassosSaga.EnfileirarExecucao, PassosSaga.Falha, true));
    }

    [Fact]
    public async Task PagamentoEstornado_EmCompensando_CancelaOs()
    {
        await LevarSagaAteAsync("Compensando");

        await PublicarSemErroAsync(Mensagem("PagamentoEstornado"));

        await DeveEstarEmAsync("Cancelada");
        _passos.Verify(p => p.CancelarAsync(_id, "Oficina sem capacidade", It.IsAny<CancellationToken>()), Times.Once);
        _registrador.Passos.Should().Equal((PassosSaga.EstornarPagamento, PassosSaga.Compensado, true));
    }

    [Fact]
    public async Task EstornoFalhou_PermaneceCompensandoERegistraCompensacaoFalhou()
    {
        await LevarSagaAteAsync("Compensando");

        await PublicarSemErroAsync(new EstornoFalhou { OrdemServicoId = _id, OcorridoEm = DateTime.UtcNow, Motivo = "gateway indisponivel" });

        await DeveEstarEmAsync("Compensando");
        Saga().Motivo.Should().Be("Estorno falhou: gateway indisponivel");
        _passos.VerifyNoOtherCalls();
        _registrador.Passos.Should().Contain((PassosSaga.EstornarPagamento, PassosSaga.CompensacaoFalhou, true));
    }

    [Theory]
    [InlineData("AguardandoOrcamento")]
    [InlineData("AguardandoAprovacao")]
    [InlineData("AguardandoPagamento")]
    public async Task CancelamentoSolicitado_AntesDoPagamento_EnviaCancelarOrcamentoECancela(string estado)
    {
        await LevarSagaAteAsync(estado);

        await PublicarSemErroAsync(new CancelamentoSolicitado { OrdemServicoId = _id, OcorridoEm = DateTime.UtcNow, Motivo = "Cliente desistiu" });

        await DeveEstarEmAsync("Cancelada");
        (await _harness.Sent.Any<CancelarOrcamento>()).Should().BeTrue();
        var enviado = _harness.Sent.Select<CancelarOrcamento>().Single();
        enviado.Context.DestinationAddress!.AbsolutePath.Should().EndWith("teste-oficina-billing-comandos");
        enviado.Context.Message.Motivo.Should().Be("Cliente desistiu");
        _passos.Verify(p => p.CancelarAsync(_id, "Cliente desistiu", It.IsAny<CancellationToken>()), Times.Once);
        _passos.VerifyNoOtherCalls();
        Saga().Motivo.Should().Be("Cliente desistiu");
        _registrador.Passos.Should().Equal((PassosSaga.Cancelamento, PassosSaga.Compensado, true));
    }

    [Fact]
    public async Task CancelamentoSolicitado_AposPagamento_EIgnoradoERegistrado()
    {
        await LevarSagaAteAsync("AguardandoExecucao");
        var enviadosAntes = TotalEnviados();

        await PublicarSemErroAsync(new CancelamentoSolicitado { OrdemServicoId = _id, OcorridoEm = DateTime.UtcNow, Motivo = "Cliente desistiu" });

        await DeveEstarEmAsync("AguardandoExecucao");
        _passos.VerifyNoOtherCalls();
        TotalEnviados().Should().Be(enviadosAntes);
        _registrador.Passos.Should().Equal((PassosSaga.Cancelamento, PassosSaga.Ignorado, false));
    }

    [Theory]
    [InlineData("EmExecucao")]
    [InlineData("Compensando")]
    public async Task CancelamentoSolicitado_EmExecucaoOuCompensando_EIgnoradoERegistrado(string estado)
    {
        await LevarSagaAteAsync(estado);

        await PublicarSemErroAsync(new CancelamentoSolicitado { OrdemServicoId = _id, OcorridoEm = DateTime.UtcNow, Motivo = "Cliente desistiu" });

        await DeveEstarEmAsync(estado);
        _passos.VerifyNoOtherCalls();
        _registrador.Passos.Should().Equal((PassosSaga.Cancelamento, PassosSaga.Ignorado, false));
    }

    [Fact]
    public async Task PagamentoConfirmado_AposCancelamento_EnviaEstornarPagamento()
    {
        await LevarSagaAteAsync("AguardandoPagamento");
        await PublicarSemErroAsync(new CancelamentoSolicitado { OrdemServicoId = _id, OcorridoEm = DateTime.UtcNow, Motivo = "Cliente desistiu" });
        await DeveEstarEmAsync("Cancelada");
        _passos.Invocations.Clear();
        _registrador.Limpar();

        await PublicarSemErroAsync(Mensagem("PagamentoConfirmado"));

        await DeveEstarEmAsync("Compensando");
        (await _harness.Sent.Any<EstornarPagamento>()).Should().BeTrue();
        _harness.Sent.Select<EstornarPagamento>().Single().Context.Message.Motivo
            .Should().Be("Pagamento confirmado após cancelamento da OS");
        _harness.Sent.Select<IniciarExecucao>().Should().BeEmpty();
        _passos.VerifyNoOtherCalls(); // nunca ConfirmarPagamentoAsync (baixa de estoque) para OS cancelada
        Saga().PagamentoId.Should().Be(PagamentoId);
        _registrador.Passos.Should().Equal((PassosSaga.EstornarPagamento, PassosSaga.Falha, true));

        // O estorno conclui a compensação e a saga volta a Cancelada.
        await PublicarSemErroAsync(Mensagem("PagamentoEstornado"));
        await DeveEstarEmAsync("Cancelada");
        _passos.Verify(p => p.CancelarAsync(_id, "Cliente desistiu", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PagamentoConfirmado_DuplicadoDePagamentoJaEstornado_EmCancelada_EIgnorado()
    {
        await LevarSagaAteAsync("Compensando"); // pagamento PagamentoId confirmado e estorno pedido
        await PublicarSemErroAsync(Mensagem("PagamentoEstornado"));
        await DeveEstarEmAsync("Cancelada");
        _passos.Invocations.Clear();
        _registrador.Limpar();
        var enviadosAntes = TotalEnviados();

        await PublicarSemErroAsync(Mensagem("PagamentoConfirmado"));

        await DeveEstarEmAsync("Cancelada");
        TotalEnviados().Should().Be(enviadosAntes);
        _passos.VerifyNoOtherCalls();
        _registrador.Passos.Should().BeEmpty();
    }

    [Fact]
    public async Task PagamentoConfirmado_MesmoPagamento_EmCompensando_EIgnorado()
    {
        await LevarSagaAteAsync("Compensando");
        var enviadosAntes = TotalEnviados();

        await PublicarSemErroAsync(Mensagem("PagamentoConfirmado"));

        await DeveEstarEmAsync("Compensando");
        TotalEnviados().Should().Be(enviadosAntes);
        _passos.VerifyNoOtherCalls();
        _registrador.Passos.Should().BeEmpty();
    }

    [Fact]
    public async Task PagamentoConfirmado_OutroPagamento_EmCompensando_EnviaEstornarPagamentoSemBaixarEstoque()
    {
        await LevarSagaAteAsync("Compensando");

        await PublicarSemErroAsync(new PagamentoConfirmado
        {
            OrdemServicoId = _id, OcorridoEm = DateTime.UtcNow, PagamentoId = "9999999999", Valor = 191m, Meio = "pix",
        });

        await DeveEstarEmAsync("Compensando");
        (await _harness.Sent.Any<EstornarPagamento>(x => x.Context.Message.Motivo == "Pagamento confirmado após cancelamento da OS"))
            .Should().BeTrue();
        _passos.VerifyNoOtherCalls();
        _harness.Sent.Select<IniciarExecucao>().Should().HaveCount(1); // só o da sequência feliz
        _registrador.Registros.Should().Equal((PassosSaga.EstornarPagamento, PassosSaga.Falha, true, "9999999999"));
    }

    // ---------------------------------------------------------------- idempotência e ordem

    [Fact]
    public async Task EventoDuplicado_NaoReexecutaPasso()
    {
        await LevarSagaAteAsync("AguardandoOrcamento");

        await PublicarSemErroAsync(Mensagem("OrcamentoGerado"));
        await PublicarSemErroAsync(Mensagem("OrcamentoGerado"));

        await DeveEstarEmAsync("AguardandoAprovacao");
        _passos.Verify(p => p.RegistrarOrcamentoGeradoAsync(_id, It.IsAny<CancellationToken>()), Times.Once);
        _registrador.Passos.Should().ContainSingle();
    }

    [Fact]
    public async Task StatusExecucaoAlterado_AntesDaEnfileirada_FalhaParaRedeliverySemEfeito()
    {
        await LevarSagaAteAsync("AguardandoExecucao");
        var adiantado = new StatusExecucaoAlterado { OrdemServicoId = _id, OcorridoEm = DateTime.UtcNow, StatusExecucao = StatusExecucao.EmDiagnostico };

        var consumido = await PublicarAsync(adiantado);

        consumido.Exception.Should().NotBeNull();
        _harness.Consumed.Select<StatusExecucaoAlterado>(x => x.Exception != null).Any().Should().BeTrue();
        _passos.Verify(p => p.RegistrarStatusExecucaoAsync(It.IsAny<Guid>(), It.IsAny<EtapaExecucao>(), It.IsAny<CancellationToken>()), Times.Never);
        await DeveEstarEmAsync("AguardandoExecucao");

        await PublicarSemErroAsync(Mensagem("ExecucaoEnfileirada"));
        await PublicarSemErroAsync(adiantado); // a reentrega

        await DeveEstarEmAsync("EmExecucao");
        _passos.Verify(p => p.RegistrarStatusExecucaoAsync(_id, EtapaExecucao.EmDiagnostico, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [MemberData(nameof(ParesAdiantados))]
    public async Task EventoAdiantado_NaoDeclaradoNoEstado_FalhaSemChamarPasso(string estado, string evento)
    {
        await LevarSagaAteAsync(estado);
        var enviadosAntes = TotalEnviados();

        var consumido = await PublicarAsync(Mensagem(evento));

        consumido.Exception.Should().NotBeNull("evento de etapa futura deve falhar para ser reentregue");
        await DeveEstarEmAsync(estado);
        _passos.VerifyNoOtherCalls();
        TotalEnviados().Should().Be(enviadosAntes);
        _registrador.Passos.Should().BeEmpty();
    }

    [Theory]
    [InlineData("EmExecucao")]
    [InlineData("Concluida")]
    [InlineData("Compensando")]
    [InlineData("Cancelada")]
    public async Task ExecucaoEnfileiradaRepublicada_EmEstadosPosteriores_EIgnoradaSemErro(string estado)
    {
        await LevarSagaAteAsync(estado, limparRegistros: false);

        await PublicarSemErroAsync(new ExecucaoEnfileirada { OrdemServicoId = _id, OcorridoEm = DateTime.UtcNow, PosicaoFila = 0 });

        await DeveEstarEmAsync(estado);
        _harness.Consumed.Select(x => x.Exception != null).Should().BeEmpty();
        _passos.Verify(p => p.RegistrarExecucaoEnfileiradaAsync(_id, It.IsAny<CancellationToken>()), Times.AtMostOnce);
    }

    [Theory]
    [MemberData(nameof(ParesIgnorados))]
    public async Task MatrizDeIgnore_EventoJaPassado_NaoFalhaNemTemEfeito(string estado, string evento)
    {
        await LevarSagaAteAsync(estado);
        var antes = Instantaneo();
        var enviadosAntes = TotalEnviados();

        await PublicarSemErroAsync(Mensagem(evento));

        await DeveEstarEmAsync(estado);
        _harness.Consumed.Select(x => x.Exception != null).Should().BeEmpty();
        _passos.VerifyNoOtherCalls();
        TotalEnviados().Should().Be(enviadosAntes);
        _registrador.Passos.Should().BeEmpty();
        Instantaneo().Should().Be(antes);
    }

    [Fact]
    public void MatrizDeIgnore_ClassificaCadaEventoDeCadaEstadoUmaUnicaVez()
    {
        var ignorados = OrdemServicoSaga.EventosIgnoradosPorEstado;

        ignorados.Keys.Should().BeEquivalentTo(EventosTratadosPorEstado.Keys);
        foreach (var estado in EventosTratadosPorEstado.Keys)
        {
            var classificados = ignorados[estado]
                .Concat(EventosTratadosPorEstado[estado])
                .Concat(EventosAdiantadosPorEstado[estado])
                .ToList();
            classificados.Should().OnlyHaveUniqueItems($"estado {estado}");
            classificados.Should().BeEquivalentTo(TodosOsEventos, $"estado {estado}");
        }
    }

    [Fact]
    public async Task EventosTardios_EmEstadoFinal_SaoIgnorados()
    {
        await LevarSagaAteAsync("Concluida", limparRegistros: false);

        await PublicarSemErroAsync(Mensagem("ExecucaoFinalizada"));

        await DeveEstarEmAsync("Concluida");
        _harness.Published.Select<Fault>().Should().BeEmpty();
        _passos.Verify(p => p.RegistrarExecucaoFinalizadaAsync(_id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EventoSemInstancia_Falha()
    {
        var consumido = await PublicarAsync(new OrcamentoGerado { OrdemServicoId = Guid.NewGuid(), OcorridoEm = DateTime.UtcNow, OrcamentoId = Guid.NewGuid(), ValorTotal = 10m });

        consumido.Exception.Should().NotBeNull();
        (await _harness.Published.Any<Fault<OrcamentoGerado>>()).Should().BeTrue();
        _passos.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ComandosEnviados_NaoDefinemFaultAddress()
    {
        await LevarSagaAteAsync("Compensando");
        await LevarSagaCanceladaComCancelarOrcamentoAsync(Guid.NewGuid());

        var enviados = _harness.Sent.Select(_ => true).ToList();

        enviados.Select(e => e.MessageType).Should().Contain(new[]
        {
            typeof(GerarOrcamento), typeof(IniciarExecucao), typeof(EstornarPagamento), typeof(CancelarOrcamento),
        });
        enviados.Should().OnlyContain(e => e.Context.FaultAddress == null && e.Context.ResponseAddress == null);
    }

    // ---------------------------------------------------------------- helpers

    private async Task LevarSagaCanceladaComCancelarOrcamentoAsync(Guid outraOs)
    {
        await PublicarSemErroAsync(new OrdemServicoAberta { OrdemServicoId = outraOs, OcorridoEm = DateTime.UtcNow });
        await PublicarSemErroAsync(new CancelamentoSolicitado { OrdemServicoId = outraOs, OcorridoEm = DateTime.UtcNow, Motivo = "Cliente desistiu" });
        (await _harness.Sent.Any<CancelarOrcamento>()).Should().BeTrue();
    }

    /// <summary>
    /// Publica a sequência feliz (ou de compensação) até o estado; depois zera as invocações do mock
    /// e o registrador para o teste verificar só o efeito do evento sob teste.
    /// </summary>
    private async Task LevarSagaAteAsync(string estado, bool limparRegistros = true)
    {
        var sequencia = estado switch
        {
            "AguardandoOrcamento" => new[] { "OrdemServicoAberta" },
            "AguardandoAprovacao" => new[] { "OrdemServicoAberta", "OrcamentoGerado" },
            "AguardandoPagamento" => new[] { "OrdemServicoAberta", "OrcamentoGerado", "OrcamentoAprovado" },
            "AguardandoExecucao" => new[] { "OrdemServicoAberta", "OrcamentoGerado", "OrcamentoAprovado", "PagamentoConfirmado" },
            "EmExecucao" => new[] { "OrdemServicoAberta", "OrcamentoGerado", "OrcamentoAprovado", "PagamentoConfirmado", "ExecucaoEnfileirada" },
            "Concluida" => new[] { "OrdemServicoAberta", "OrcamentoGerado", "OrcamentoAprovado", "PagamentoConfirmado", "ExecucaoEnfileirada", "ExecucaoFinalizada" },
            "Compensando" => new[] { "OrdemServicoAberta", "OrcamentoGerado", "OrcamentoAprovado", "PagamentoConfirmado", "ExecucaoRecusada" },
            "Cancelada" => new[] { "OrdemServicoAberta", "OrcamentoFalhou" },
            _ => throw new ArgumentOutOfRangeException(nameof(estado), estado, null),
        };

        foreach (var evento in sequencia)
            await PublicarSemErroAsync(Mensagem(evento));

        await DeveEstarEmAsync(estado);
        if (limparRegistros)
        {
            _passos.Invocations.Clear();
            _registrador.Limpar();
        }
    }

    private object Mensagem(string evento)
    {
        var agora = DateTime.UtcNow;
        return evento switch
        {
            "OrdemServicoAberta" => new OrdemServicoAberta { OrdemServicoId = _id, OcorridoEm = agora },
            "CancelamentoSolicitado" => new CancelamentoSolicitado { OrdemServicoId = _id, OcorridoEm = agora, Motivo = "Cliente desistiu" },
            "OrcamentoGerado" => new OrcamentoGerado { OrdemServicoId = _id, OcorridoEm = agora, OrcamentoId = Guid.NewGuid(), ValorTotal = 191m },
            "OrcamentoFalhou" => new OrcamentoFalhou { OrdemServicoId = _id, OcorridoEm = agora, Motivo = "Orcamento sem itens" },
            "OrcamentoAprovado" => new OrcamentoAprovado { OrdemServicoId = _id, OcorridoEm = agora, OrcamentoId = Guid.NewGuid(), LinkPagamento = "https://mp.exemplo/p/1" },
            "OrcamentoRejeitado" => new OrcamentoRejeitado { OrdemServicoId = _id, OcorridoEm = agora, OrcamentoId = Guid.NewGuid(), Motivo = "Cliente achou caro" },
            "PagamentoConfirmado" => new PagamentoConfirmado { OrdemServicoId = _id, OcorridoEm = agora, PagamentoId = PagamentoId, Valor = 191m, Meio = "pix" },
            "PagamentoRecusado" => new PagamentoRecusado { OrdemServicoId = _id, OcorridoEm = agora, PagamentoId = PagamentoId, Motivo = "cartao_recusado" },
            "ExecucaoEnfileirada" => new ExecucaoEnfileirada { OrdemServicoId = _id, OcorridoEm = agora, PosicaoFila = 1 },
            "ExecucaoRecusada" => new ExecucaoRecusada { OrdemServicoId = _id, OcorridoEm = agora, Motivo = "Oficina sem capacidade" },
            "StatusExecucaoAlterado" => new StatusExecucaoAlterado { OrdemServicoId = _id, OcorridoEm = agora, StatusExecucao = StatusExecucao.EmDiagnostico },
            "ExecucaoFinalizada" => new ExecucaoFinalizada { OrdemServicoId = _id, OcorridoEm = agora, DuracaoMinutos = 42 },
            "PagamentoEstornado" => new PagamentoEstornado { OrdemServicoId = _id, OcorridoEm = agora, PagamentoId = PagamentoId, Valor = 191m },
            "EstornoFalhou" => new EstornoFalhou { OrdemServicoId = _id, OcorridoEm = agora, Motivo = "gateway indisponivel" },
            _ => throw new ArgumentOutOfRangeException(nameof(evento), evento, null),
        };
    }

    /// <summary>Publica e espera o consumo da mensagem (com ou sem exceção).</summary>
    private async Task<IReceivedMessage> PublicarAsync(object mensagem)
    {
        var messageId = NewId.NextGuid();
        await _harness.Bus.Publish(mensagem, c => c.MessageId = messageId);

        var limite = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < limite)
        {
            var consumido = _harness.Consumed.Select(x => x.Context.MessageId == messageId).SingleOrDefault();
            if (consumido is not null)
                return consumido;
            await Task.Delay(10);
        }
        throw new TimeoutException($"{mensagem.GetType().Name} não foi consumida em 10s.");
    }

    private async Task PublicarSemErroAsync(object mensagem)
    {
        var consumido = await PublicarAsync(mensagem);
        consumido.Exception.Should().BeNull($"{mensagem.GetType().Name} não deveria falhar");
    }

    private async Task DeveEstarEmAsync(string estado)
    {
        // Polling próprio: os Any/Exists do harness esperam a inatividade (~1,2 s) quando o filtro
        // não casa de primeira, e depois dela passam a responder sem esperar.
        var limite = DateTime.UtcNow.AddSeconds(5);
        while (_sagaHarness.Sagas.Contains(_id)?.EstadoAtual != estado && DateTime.UtcNow < limite)
            await Task.Delay(10);
        Saga().EstadoAtual.Should().Be(estado);
    }

    private EstadoSagaOrdemServico Saga() =>
        _sagaHarness.Sagas.Contains(_id) ?? throw new InvalidOperationException("Saga não encontrada.");

    private (string, long, string?, string?, DateTime, DateTime) Instantaneo()
    {
        var s = Saga();
        return (s.EstadoAtual, s.NumeroOs, s.PagamentoId, s.Motivo, s.CriadaEm, s.AtualizadaEm);
    }

    private int TotalEnviados() => _harness.Sent.Select(_ => true).Count();
}
