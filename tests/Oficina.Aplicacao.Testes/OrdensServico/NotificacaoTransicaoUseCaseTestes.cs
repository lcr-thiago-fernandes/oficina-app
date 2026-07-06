using FluentAssertions;
using Moq;
using Oficina.Aplicacao.OrdensServico;
using Oficina.Aplicacao.OrdensServico.Gateways;
using Oficina.Dominio.OrdensServico;
using Xunit;

namespace Oficina.Aplicacao.Testes.OrdensServico;

// Verifica que as transições simples (que persistem via IOrdemDeServicoGateway)
// notificam via INotificacaoGateway após SalvarAsync, e não notificam quando a OS não existe.
public class NotificacaoTransicaoUseCaseTestes
{
    private readonly Mock<IOrdemDeServicoGateway> _ordens = new();
    private readonly Mock<INotificacaoGateway> _notificacoes = new();

    private static OrdemDeServico OsRecebidaComItem()
    {
        var os = OrdemDeServico.Criar(Guid.NewGuid(), Guid.NewGuid());
        os.AdicionarItemServico(Guid.NewGuid(), "S", 10m, 1);
        return os;
    }

    [Fact]
    public async Task IniciarDiagnostico_DeveNotificar()
    {
        var os = OsRecebidaComItem();
        _ordens.Setup(r => r.ObterPorIdAsync(os.Id, It.IsAny<CancellationToken>())).ReturnsAsync(os);

        var resp = await new IniciarDiagnosticoUseCase(_ordens.Object, _notificacoes.Object)
            .ExecutarAsync(os.Id, default);

        resp.Should().NotBeNull();
        os.Status.Should().Be(StatusOrdemDeServico.EmDiagnostico);
        _notificacoes.Verify(n => n.NotificarMudancaDeStatusAsync(os, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EnviarOrcamentoParaAprovacao_DeveNotificar()
    {
        var os = OsRecebidaComItem();
        os.IniciarDiagnostico();
        _ordens.Setup(r => r.ObterPorIdAsync(os.Id, It.IsAny<CancellationToken>())).ReturnsAsync(os);

        var resp = await new EnviarOrcamentoParaAprovacaoUseCase(_ordens.Object, _notificacoes.Object)
            .ExecutarAsync(os.Id, default);

        resp.Should().NotBeNull();
        os.Status.Should().Be(StatusOrdemDeServico.AguardandoAprovacao);
        _notificacoes.Verify(n => n.NotificarMudancaDeStatusAsync(os, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Finalizar_DeveNotificar()
    {
        var os = OsRecebidaComItem();
        os.IniciarDiagnostico();
        os.EnviarOrcamentoParaAprovacao();
        os.Aprovar();
        os.IniciarExecucao();
        _ordens.Setup(r => r.ObterPorIdAsync(os.Id, It.IsAny<CancellationToken>())).ReturnsAsync(os);

        var resp = await new FinalizarOrdemUseCase(_ordens.Object, _notificacoes.Object)
            .ExecutarAsync(os.Id, default);

        resp.Should().NotBeNull();
        os.Status.Should().Be(StatusOrdemDeServico.Finalizada);
        _notificacoes.Verify(n => n.NotificarMudancaDeStatusAsync(os, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Entregar_DeveNotificar()
    {
        var os = OsRecebidaComItem();
        os.IniciarDiagnostico();
        os.EnviarOrcamentoParaAprovacao();
        os.Aprovar();
        os.IniciarExecucao();
        os.Finalizar();
        _ordens.Setup(r => r.ObterPorIdAsync(os.Id, It.IsAny<CancellationToken>())).ReturnsAsync(os);

        var resp = await new EntregarOrdemUseCase(_ordens.Object, _notificacoes.Object)
            .ExecutarAsync(os.Id, default);

        resp.Should().NotBeNull();
        os.Status.Should().Be(StatusOrdemDeServico.Entregue);
        _notificacoes.Verify(n => n.NotificarMudancaDeStatusAsync(os, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task IniciarDiagnostico_OsInexistente_NaoNotifica()
    {
        _ordens.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((OrdemDeServico?)null);

        var resp = await new IniciarDiagnosticoUseCase(_ordens.Object, _notificacoes.Object)
            .ExecutarAsync(Guid.NewGuid(), default);

        resp.Should().BeNull();
        _notificacoes.Verify(n => n.NotificarMudancaDeStatusAsync(It.IsAny<OrdemDeServico>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
