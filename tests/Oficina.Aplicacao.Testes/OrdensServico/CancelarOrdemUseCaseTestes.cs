using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Oficina.Aplicacao.Clientes.Gateways;
using Oficina.Aplicacao.Estoque.Gateways;
using Oficina.Aplicacao.OrdensServico;
using Oficina.Aplicacao.OrdensServico.Dtos;
using Oficina.Aplicacao.OrdensServico.Gateways;
using Oficina.Aplicacao.OrdensServico.Saga;
using Oficina.Aplicacao.OrdensServico.Saga.Dtos;
using Oficina.Aplicacao.OrdensServico.Telemetria;
using Oficina.Dominio.Estoque;
using Oficina.Dominio.OrdensServico;
using Oficina.Testes.Compartilhado;
using Xunit;

namespace Oficina.Aplicacao.Testes.OrdensServico;

public class CancelarOrdemUseCaseTestes
{
    private readonly Mock<IOrdemDeServicoGateway> _ordens = new();
    private readonly Mock<ISagaOrdemServicoGateway> _saga = new();
    private readonly Mock<IPassosSagaOrdemServico> _passos = new();

    private CancelarOrdemUseCase Sut => new(_ordens.Object, _saga.Object, _passos.Object);

    private OrdemDeServico Os(StatusOrdemDeServico status, bool coordenadaPorSaga = true)
    {
        var os = OrdemEmStatus.Criar(status);
        if (coordenadaPorSaga) os.MarcarCoordenadaPorSaga();
        _ordens.Setup(o => o.ObterPorIdAsync(os.Id, It.IsAny<CancellationToken>())).ReturnsAsync(os);
        return os;
    }

    private void SagaEm(Guid id, string estado) =>
        _saga.Setup(s => s.ObterEstadoAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EstadoSagaResponse(id, 1, estado, null, null,
                DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, Array.Empty<PassoSagaResponse>()));

    private void VerificarNadaSolicitado()
    {
        _saga.Verify(s => s.SolicitarCancelamentoAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _passos.Verify(p => p.CancelarAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _ordens.Verify(o => o.SalvarAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(StatusOrdemDeServico.Recebida, "AguardandoOrcamento")]
    [InlineData(StatusOrdemDeServico.AguardandoAprovacao, "AguardandoAprovacao")]
    [InlineData(StatusOrdemDeServico.AguardandoPagamento, "AguardandoPagamento")]
    public async Task Cancelar_EmStatusPermitido_SolicitaCancelamentoESalva(StatusOrdemDeServico status, string estado)
    {
        var os = Os(status);
        SagaEm(os.Id, estado);

        var resp = await Sut.ExecutarAsync(os.Id, "  motivo  ", default);

        resp.Should().NotBeNull();
        resp!.Ordem.Should().BeSameAs(os);
        resp.Forma.Should().Be(FormaCancelamento.Solicitado);
        os.Status.Should().Be(status);
        _saga.Verify(s => s.SolicitarCancelamentoAsync(os.Id, "motivo", It.IsAny<CancellationToken>()), Times.Once);
        _ordens.Verify(o => o.SalvarAsync(It.IsAny<CancellationToken>()), Times.Once);
        _passos.Verify(p => p.CancelarAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Cancelar_AposPagamento_LancaTransicaoInvalida(bool coordenadaPorSaga)
    {
        var os = Os(StatusOrdemDeServico.EmFilaExecucao, coordenadaPorSaga);

        var act = async () => await Sut.ExecutarAsync(os.Id, "motivo", default);

        await act.Should().ThrowAsync<TransicaoDeStatusInvalidaException>();
        VerificarNadaSolicitado();
    }

    [Theory]
    [InlineData("AguardandoExecucao")]
    [InlineData("Compensando")]
    [InlineData("Cancelada")]
    public async Task Cancelar_AposPagamentoConfirmado_Lanca(string estado)
    {
        var os = Os(StatusOrdemDeServico.AguardandoPagamento);
        SagaEm(os.Id, estado);

        var act = async () => await Sut.ExecutarAsync(os.Id, "motivo", default);

        await act.Should().ThrowAsync<TransicaoDeStatusInvalidaException>();
        VerificarNadaSolicitado();
    }

    [Fact]
    public async Task Cancelar_SagaAindaNaoCriada_Permite()
    {
        // OS da Fase 4 cuja OrdemServicoAberta ainda está no outbox.
        var os = Os(StatusOrdemDeServico.Recebida);
        _saga.Setup(s => s.ObterEstadoAsync(os.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((EstadoSagaResponse?)null);

        var resp = await Sut.ExecutarAsync(os.Id, "motivo", default);

        resp!.Forma.Should().Be(FormaCancelamento.Solicitado);
        _saga.Verify(s => s.SolicitarCancelamentoAsync(os.Id, "motivo", It.IsAny<CancellationToken>()), Times.Once);
        _ordens.Verify(o => o.SalvarAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(StatusOrdemDeServico.Recebida)]
    [InlineData(StatusOrdemDeServico.AguardandoAprovacao)]
    [InlineData(StatusOrdemDeServico.AguardandoPagamento)]
    public async Task Cancelar_OsLegadaSemSaga_CancelaNaHoraPelosPassos(StatusOrdemDeServico status)
    {
        // OS da Fase 3: nunca terá saga, então CancelamentoSolicitado iria para a _error sem cancelar.
        var os = Os(status, coordenadaPorSaga: false);

        var resp = await Sut.ExecutarAsync(os.Id, "  motivo  ", default);

        resp!.Forma.Should().Be(FormaCancelamento.Concluido);
        resp.Ordem.Should().BeSameAs(os);
        _passos.Verify(p => p.CancelarAsync(os.Id, "motivo", It.IsAny<CancellationToken>()), Times.Once);
        _saga.Verify(s => s.SolicitarCancelamentoAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _saga.Verify(s => s.ObterEstadoAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Cancelar_OsLegadaComSaidaManual_CancelaSemReporNemLancar()
    {
        // Ponta a ponta com os passos reais: a saída manual da Fase 3 (sem reserva) lê "Confirmada",
        // e o cancelamento síncrono não pode lançar 422 do Repor.
        var pecas = new Mock<IPecaGateway>();
        var peca = Peca.Criar(Sku.Criar("FIL-001"), "Filtro", 50m);
        peca.RegistrarEntrada(10, "Compra");
        var os = OrdemDeServico.Criar(Guid.NewGuid(), Guid.NewGuid());
        os.AdicionarItemPeca(peca.Id, peca.Nome, 50m, 3);
        peca.RegistrarSaida(3, "Saída manual Fase 3", os.Id);
        _ordens.Setup(o => o.ObterPorIdAsync(os.Id, It.IsAny<CancellationToken>())).ReturnsAsync(os);
        pecas.Setup(p => p.ObterPorIdAsync(peca.Id, It.IsAny<CancellationToken>())).ReturnsAsync(peca);
        var passos = new PassosSagaOrdemServico(_ordens.Object, pecas.Object, Mock.Of<IClienteGateway>(),
            Mock.Of<INotificacaoGateway>(), Mock.Of<IPublicadorEventoOs>(), NullLogger<PassosSagaOrdemServico>.Instance);

        var resp = await new CancelarOrdemUseCase(_ordens.Object, _saga.Object, passos)
            .ExecutarAsync(os.Id, "Cliente desistiu", default);

        resp!.Forma.Should().Be(FormaCancelamento.Concluido);
        os.Status.Should().Be(StatusOrdemDeServico.Cancelada);
        os.MotivoCancelamento.Should().Be("Cliente desistiu");
        peca.SaldoAtual.Should().Be(7);
        pecas.Verify(p => p.MarcarMovimentacaoComoNova(It.IsAny<MovimentacaoEstoque>()), Times.Never);
        _ordens.Verify(o => o.SalvarAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Cancelar_Inexistente_RetornaNull()
    {
        _ordens.Setup(o => o.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((OrdemDeServico?)null);

        var resp = await Sut.ExecutarAsync(Guid.NewGuid(), "motivo", default);

        resp.Should().BeNull();
        VerificarNadaSolicitado();
    }
}
