using FluentAssertions;
using Moq;
using Oficina.Aplicacao.OrdensServico;
using Oficina.Aplicacao.OrdensServico.Gateways;
using Oficina.Aplicacao.OrdensServico.Telemetria;
using Oficina.Dominio.OrdensServico;
using Xunit;

namespace Oficina.Aplicacao.Testes.OrdensServico;

public class RegistrarDecisaoDeOrcamentoUseCaseTestes
{
    private readonly Mock<IOrdemDeServicoGateway> _ordens = new();
    private readonly Mock<INotificacaoGateway> _notificacoes = new();
    private readonly Mock<IPublicadorEventoOs> _publicador = new();

    private RegistrarDecisaoDeOrcamentoUseCase CriarUseCase() =>
        new(_ordens.Object, _notificacoes.Object, _publicador.Object);

    private static OrdemDeServico OsAguardandoAprovacao()
    {
        var os = OrdemDeServico.Criar(Guid.NewGuid(), Guid.NewGuid());
        os.AdicionarItemServico(Guid.NewGuid(), "S", 10m, 1);
        os.IniciarDiagnostico();
        os.EnviarOrcamentoParaAprovacao();
        return os;
    }

    [Fact]
    public async Task Executar_Aprovado_DeveAprovarPersistirENotificar()
    {
        var os = OsAguardandoAprovacao();
        _ordens.Setup(o => o.ObterPorIdAsync(os.Id, It.IsAny<CancellationToken>())).ReturnsAsync(os);

        var resp = await CriarUseCase().ExecutarAsync(os.Id, true, default);

        resp.Should().NotBeNull();
        os.OrcamentoAprovadoEm.Should().NotBeNull();
        os.Status.Should().Be(StatusOrdemDeServico.AguardandoAprovacao); // Aprovar só carimba a data
        _ordens.Verify(o => o.SalvarAsync(It.IsAny<CancellationToken>()), Times.Once);
        _notificacoes.Verify(n => n.NotificarMudancaDeStatusAsync(os, It.IsAny<CancellationToken>()), Times.Once);
        // Aprovar() não muda o Status nem gera entrada de Historico — não há
        // transição para publicar (o evento de negócio fica a cargo da rejeição).
        _publicador.Verify(p => p.Publicar(It.IsAny<EventoOrdemServico>()), Times.Never);
    }

    [Fact]
    public async Task Executar_Recusado_DeveRejeitarECancelar()
    {
        var os = OsAguardandoAprovacao();
        _ordens.Setup(o => o.ObterPorIdAsync(os.Id, It.IsAny<CancellationToken>())).ReturnsAsync(os);

        var resp = await CriarUseCase().ExecutarAsync(os.Id, false, default);

        resp.Should().NotBeNull();
        os.Status.Should().Be(StatusOrdemDeServico.Cancelada);
        os.OrcamentoRejeitadoEm.Should().NotBeNull();
        _notificacoes.Verify(n => n.NotificarMudancaDeStatusAsync(os, It.IsAny<CancellationToken>()), Times.Once);
        _publicador.Verify(p => p.Publicar(
            It.Is<EventoOrdemServico>(e => e.StatusNovo == "Cancelada" && e.Resultado == EventoOrdemServico.ResultadoSucesso)),
            Times.Once);
    }

    [Fact]
    public async Task Executar_OsInexistente_DeveRetornarNull()
    {
        _ordens.Setup(o => o.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((OrdemDeServico?)null);

        var resp = await CriarUseCase().ExecutarAsync(Guid.NewGuid(), true, default);

        resp.Should().BeNull();
        _ordens.Verify(o => o.SalvarAsync(It.IsAny<CancellationToken>()), Times.Never);
        _notificacoes.Verify(n => n.NotificarMudancaDeStatusAsync(It.IsAny<OrdemDeServico>(), It.IsAny<CancellationToken>()), Times.Never);
        _publicador.Verify(p => p.Publicar(It.IsAny<EventoOrdemServico>()), Times.Never);
    }

    [Fact]
    public async Task Executar_EstadoInvalido_DevePropagarTransicaoInvalida()
    {
        // OS em Recebida — não pode aprovar
        var os = OrdemDeServico.Criar(Guid.NewGuid(), Guid.NewGuid());
        _ordens.Setup(o => o.ObterPorIdAsync(os.Id, It.IsAny<CancellationToken>())).ReturnsAsync(os);

        var act = async () => await CriarUseCase().ExecutarAsync(os.Id, true, default);

        await act.Should().ThrowAsync<TransicaoDeStatusInvalidaException>();
        _notificacoes.Verify(n => n.NotificarMudancaDeStatusAsync(It.IsAny<OrdemDeServico>(), It.IsAny<CancellationToken>()), Times.Never);
        // Erro de negócio (4xx) NÃO é falha de processamento: publicar 'Falha' aqui
        // dispararia o alerta Critical da Fase 3 a cada requisição inválida do cliente.
        _publicador.Verify(p => p.Publicar(It.IsAny<EventoOrdemServico>()), Times.Never);
    }
}
