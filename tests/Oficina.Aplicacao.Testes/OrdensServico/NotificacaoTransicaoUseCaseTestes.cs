using FluentAssertions;
using Moq;
using Oficina.Aplicacao.OrdensServico;
using Oficina.Aplicacao.OrdensServico.Gateways;
using Oficina.Aplicacao.OrdensServico.Telemetria;
using Oficina.Dominio.OrdensServico;
using Oficina.Testes.Compartilhado;
using Xunit;

namespace Oficina.Aplicacao.Testes.OrdensServico;

// Verifica que a transição por HTTP que restou na Fase 4 (entrega) notifica via
// INotificacaoGateway após SalvarAsync, e não notifica quando a OS não existe.
// Também verifica que a transição publica o evento de telemetria (Task 10)
// depois de SalvarAsync ter sido bem-sucedido.
public class NotificacaoTransicaoUseCaseTestes
{
    private readonly Mock<IOrdemDeServicoGateway> _ordens = new();
    private readonly Mock<INotificacaoGateway> _notificacoes = new();
    private readonly Mock<IPublicadorEventoOs> _publicador = new();

    private EntregarOrdemUseCase Sut => new(_ordens.Object, _notificacoes.Object, _publicador.Object);

    [Fact]
    public async Task Entregar_DeveNotificar()
    {
        var os = OrdemEmStatus.Criar(StatusOrdemDeServico.Finalizada);
        _ordens.Setup(r => r.ObterPorIdAsync(os.Id, It.IsAny<CancellationToken>())).ReturnsAsync(os);

        var resp = await Sut.ExecutarAsync(os.Id, default);

        resp.Should().NotBeNull();
        os.Status.Should().Be(StatusOrdemDeServico.Entregue);
        _ordens.Verify(r => r.SalvarAsync(It.IsAny<CancellationToken>()), Times.Once);
        _notificacoes.Verify(n => n.NotificarMudancaDeStatusAsync(os, It.IsAny<CancellationToken>()), Times.Once);
        _publicador.Verify(p => p.Publicar(
            It.Is<EventoOrdemServico>(e => e.StatusAnterior == "Finalizada" && e.StatusNovo == "Entregue")), Times.Once);
    }

    [Fact]
    public async Task Entregar_OsInexistente_NaoNotifica()
    {
        _ordens.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((OrdemDeServico?)null);

        var resp = await Sut.ExecutarAsync(Guid.NewGuid(), default);

        resp.Should().BeNull();
        _notificacoes.Verify(n => n.NotificarMudancaDeStatusAsync(It.IsAny<OrdemDeServico>(), It.IsAny<CancellationToken>()), Times.Never);
        _publicador.Verify(p => p.Publicar(It.IsAny<EventoOrdemServico>()), Times.Never);
    }

    [Fact]
    public async Task Entregar_TransicaoInvalida_PropagaSemPublicarEvento()
    {
        // OS ainda em EmReparo — Entregar() lança TransicaoDeStatusInvalidaException.
        var os = OrdemEmStatus.Criar(StatusOrdemDeServico.EmReparo);
        _ordens.Setup(r => r.ObterPorIdAsync(os.Id, It.IsAny<CancellationToken>())).ReturnsAsync(os);

        var act = async () => await Sut.ExecutarAsync(os.Id, default);

        await act.Should().ThrowAsync<TransicaoDeStatusInvalidaException>();
        _ordens.Verify(r => r.SalvarAsync(It.IsAny<CancellationToken>()), Times.Never);
        _notificacoes.Verify(n => n.NotificarMudancaDeStatusAsync(It.IsAny<OrdemDeServico>(), It.IsAny<CancellationToken>()), Times.Never);
        // Transição inválida é 422 (erro do cliente), não falha de processamento:
        // publicar 'Falha' faria o alerta Critical disparar a cada requisição errada
        // — inclusive a do vídeo de demonstração.
        _publicador.Verify(p => p.Publicar(It.IsAny<EventoOrdemServico>()), Times.Never);
    }
}
