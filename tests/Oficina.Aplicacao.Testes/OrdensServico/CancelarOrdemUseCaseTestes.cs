using FluentAssertions;
using Moq;
using Oficina.Aplicacao.OrdensServico;
using Oficina.Aplicacao.OrdensServico.Gateways;
using Oficina.Aplicacao.OrdensServico.Saga;
using Oficina.Aplicacao.OrdensServico.Saga.Dtos;
using Oficina.Dominio.OrdensServico;
using Oficina.Testes.Compartilhado;
using Xunit;

namespace Oficina.Aplicacao.Testes.OrdensServico;

public class CancelarOrdemUseCaseTestes
{
    private readonly Mock<IOrdemDeServicoGateway> _ordens = new();
    private readonly Mock<ISagaOrdemServicoGateway> _saga = new();

    private CancelarOrdemUseCase Sut => new(_ordens.Object, _saga.Object);

    private OrdemDeServico Os(StatusOrdemDeServico status)
    {
        var os = OrdemEmStatus.Criar(status);
        _ordens.Setup(o => o.ObterPorIdAsync(os.Id, It.IsAny<CancellationToken>())).ReturnsAsync(os);
        return os;
    }

    private void SagaEm(Guid id, string estado) =>
        _saga.Setup(s => s.ObterEstadoAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EstadoSagaResponse(id, 1, estado, null, null,
                DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, Array.Empty<PassoSagaResponse>()));

    [Theory]
    [InlineData(StatusOrdemDeServico.Recebida, "AguardandoOrcamento")]
    [InlineData(StatusOrdemDeServico.AguardandoAprovacao, "AguardandoAprovacao")]
    [InlineData(StatusOrdemDeServico.AguardandoPagamento, "AguardandoPagamento")]
    public async Task Cancelar_EmStatusPermitido_SolicitaCancelamentoESalva(StatusOrdemDeServico status, string estado)
    {
        var os = Os(status);
        SagaEm(os.Id, estado);

        var resp = await Sut.ExecutarAsync(os.Id, "  motivo  ", default);

        resp.Should().BeSameAs(os);
        os.Status.Should().Be(status);
        _saga.Verify(s => s.SolicitarCancelamentoAsync(os.Id, "motivo", It.IsAny<CancellationToken>()), Times.Once);
        _ordens.Verify(o => o.SalvarAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Cancelar_AposPagamento_LancaTransicaoInvalida()
    {
        var os = Os(StatusOrdemDeServico.EmFilaExecucao);

        var act = async () => await Sut.ExecutarAsync(os.Id, "motivo", default);

        await act.Should().ThrowAsync<TransicaoDeStatusInvalidaException>();
        _saga.Verify(s => s.SolicitarCancelamentoAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _ordens.Verify(o => o.SalvarAsync(It.IsAny<CancellationToken>()), Times.Never);
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
        _saga.Verify(s => s.SolicitarCancelamentoAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _ordens.Verify(o => o.SalvarAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Cancelar_SagaAindaNaoCriada_Permite()
    {
        var os = Os(StatusOrdemDeServico.Recebida);
        _saga.Setup(s => s.ObterEstadoAsync(os.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((EstadoSagaResponse?)null);

        await Sut.ExecutarAsync(os.Id, "motivo", default);

        _saga.Verify(s => s.SolicitarCancelamentoAsync(os.Id, "motivo", It.IsAny<CancellationToken>()), Times.Once);
        _ordens.Verify(o => o.SalvarAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Cancelar_Inexistente_RetornaNull()
    {
        _ordens.Setup(o => o.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((OrdemDeServico?)null);

        var resp = await Sut.ExecutarAsync(Guid.NewGuid(), "motivo", default);

        resp.Should().BeNull();
        _saga.Verify(s => s.SolicitarCancelamentoAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}

public class ObterSagaDaOrdemUseCaseTestes
{
    [Fact]
    public async Task Obter_DelegaAoGateway()
    {
        var id = Guid.NewGuid();
        var estado = new EstadoSagaResponse(id, 7, "AguardandoAprovacao", null, null,
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, Array.Empty<PassoSagaResponse>());
        var saga = new Mock<ISagaOrdemServicoGateway>();
        saga.Setup(s => s.ObterEstadoAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(estado);

        var resp = await new ObterSagaDaOrdemUseCase(saga.Object).ExecutarAsync(id, default);

        resp.Should().BeSameAs(estado);
    }
}

public class CancelarOrdemValidatorTestes
{
    [Theory]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("Cliente desistiu", true)]
    public void Validar_Motivo(string motivo, bool valido) =>
        new Oficina.Aplicacao.OrdensServico.Validacoes.CancelarOrdemValidator()
            .Validate(new Oficina.Aplicacao.OrdensServico.Dtos.CancelarOrdemRequest(motivo))
            .IsValid.Should().Be(valido);

    [Fact]
    public void Validar_MotivoAcimaDe500_Invalido() =>
        new Oficina.Aplicacao.OrdensServico.Validacoes.CancelarOrdemValidator()
            .Validate(new Oficina.Aplicacao.OrdensServico.Dtos.CancelarOrdemRequest(new string('x', 501)))
            .IsValid.Should().BeFalse();
}
