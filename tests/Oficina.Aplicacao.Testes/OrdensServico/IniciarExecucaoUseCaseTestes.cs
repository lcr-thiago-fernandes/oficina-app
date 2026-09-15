using FluentAssertions;
using Moq;
using Oficina.Aplicacao.Estoque.Gateways;
using Oficina.Aplicacao.OrdensServico;
using Oficina.Aplicacao.OrdensServico.Gateways;
using Oficina.Aplicacao.OrdensServico.Telemetria;
using Oficina.Dominio.Estoque;
using Oficina.Dominio.OrdensServico;
using Xunit;

namespace Oficina.Aplicacao.Testes.OrdensServico;

public class IniciarExecucaoUseCaseTestes
{
    private readonly Mock<IOrdemDeServicoGateway> _ordens = new();
    private readonly Mock<IPecaGateway> _pecas = new();
    private readonly Mock<INotificacaoGateway> _notificacoes = new();
    private readonly Mock<IPublicadorEventoOs> _publicador = new();

    private void TransacaoIdentidade()
    {
        _ordens.Setup(r => r.EmTransacaoSerializadaAsync(
            It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>()))
            .Returns<Func<CancellationToken, Task>, CancellationToken>((f, ct) => f(ct));
    }

    private IniciarExecucaoUseCase CriarUseCase() =>
        new(_ordens.Object, _pecas.Object, _notificacoes.Object, _publicador.Object);

    private static OrdemDeServico OsAprovadaCom(Peca peca, int qtd)
    {
        var os = OrdemDeServico.Criar(Guid.NewGuid(), Guid.NewGuid());
        os.AdicionarItemPeca(peca.Id, peca.Nome, peca.PrecoUnitario, qtd);
        os.IniciarDiagnostico();
        os.EnviarOrcamentoParaAprovacao();
        os.Aprovar();
        return os;
    }

    [Fact]
    public async Task Executar_ComEstoqueSuficiente_DeveIniciarExecucaoEBaixarEstoque()
    {
        var peca = Peca.Criar(Sku.Criar("ABC-123"), "Filtro", 25m);
        peca.RegistrarEntrada(10, "compra inicial");
        var os = OsAprovadaCom(peca, 3);

        _ordens.Setup(r => r.ObterPorIdAsync(os.Id, It.IsAny<CancellationToken>())).ReturnsAsync(os);
        _pecas.Setup(p => p.ObterPorIdAsync(peca.Id, It.IsAny<CancellationToken>())).ReturnsAsync(peca);
        TransacaoIdentidade();

        var resp = await CriarUseCase().ExecutarAsync(os.Id, default);

        resp.Should().NotBeNull();
        os.Status.Should().Be(StatusOrdemDeServico.EmExecucao);
        peca.SaldoAtual.Should().Be(7);
        _notificacoes.Verify(n => n.NotificarMudancaDeStatusAsync(os, It.IsAny<CancellationToken>()), Times.Once);
        _publicador.Verify(p => p.Publicar(
            It.Is<EventoOrdemServico>(e => e.StatusNovo == "EmExecucao" && e.Resultado == EventoOrdemServico.ResultadoSucesso)),
            Times.Once);
    }

    [Fact]
    public async Task Executar_ComEstoqueInsuficiente_DeveLancarSemAlterarStatusOrdemNemSaldo()
    {
        var peca = Peca.Criar(Sku.Criar("ABC-123"), "Filtro", 25m);
        peca.RegistrarEntrada(2, "compra");
        var os = OsAprovadaCom(peca, 5);

        _ordens.Setup(r => r.ObterPorIdAsync(os.Id, It.IsAny<CancellationToken>())).ReturnsAsync(os);
        _pecas.Setup(p => p.ObterPorIdAsync(peca.Id, It.IsAny<CancellationToken>())).ReturnsAsync(peca);
        TransacaoIdentidade();

        var act = async () => await CriarUseCase().ExecutarAsync(os.Id, default);

        await act.Should().ThrowAsync<SaldoInsuficienteException>();
        peca.SaldoAtual.Should().Be(2);
        _notificacoes.Verify(n => n.NotificarMudancaDeStatusAsync(It.IsAny<OrdemDeServico>(), It.IsAny<CancellationToken>()), Times.Never);
        // Erro de negócio (4xx) NÃO é falha de processamento: publicar 'Falha' aqui
        // dispararia o alerta Critical da Fase 3 a cada requisição inválida do cliente.
        _publicador.Verify(p => p.Publicar(It.IsAny<EventoOrdemServico>()), Times.Never);
    }

    [Fact]
    public async Task Executar_OsNaoAprovada_DeveLancar()
    {
        var os = OrdemDeServico.Criar(Guid.NewGuid(), Guid.NewGuid());
        os.AdicionarItemServico(Guid.NewGuid(), "S", 10m, 1);
        os.IniciarDiagnostico();
        os.EnviarOrcamentoParaAprovacao();
        // não aprovada

        _ordens.Setup(r => r.ObterPorIdAsync(os.Id, It.IsAny<CancellationToken>())).ReturnsAsync(os);
        TransacaoIdentidade();

        var act = async () => await CriarUseCase().ExecutarAsync(os.Id, default);

        await act.Should().ThrowAsync<OrcamentoNaoAprovadoException>();
        _notificacoes.Verify(n => n.NotificarMudancaDeStatusAsync(It.IsAny<OrdemDeServico>(), It.IsAny<CancellationToken>()), Times.Never);
        // Erro de negócio (4xx) NÃO é falha de processamento: publicar 'Falha' aqui
        // dispararia o alerta Critical da Fase 3 a cada requisição inválida do cliente.
        _publicador.Verify(p => p.Publicar(It.IsAny<EventoOrdemServico>()), Times.Never);
    }

    [Fact]
    public async Task Executar_OsInexistente_DeveRetornarNull()
    {
        _ordens.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((OrdemDeServico?)null);
        TransacaoIdentidade();

        var resp = await CriarUseCase().ExecutarAsync(Guid.NewGuid(), default);

        resp.Should().BeNull();
        _notificacoes.Verify(n => n.NotificarMudancaDeStatusAsync(It.IsAny<OrdemDeServico>(), It.IsAny<CancellationToken>()), Times.Never);
        _publicador.Verify(p => p.Publicar(It.IsAny<EventoOrdemServico>()), Times.Never);
    }

    /// <summary>
    /// Transação que executa o delegate com sucesso e SÓ ENTÃO falha — é o que
    /// acontece quando o CommitAsync de OrdemDeServicoDataSource lança (conflito
    /// de serialização 40001, conexão perdida). O commit fica FORA do delegate,
    /// então antes da correção este caminho não publicava evento nenhum.
    /// </summary>
    private void TransacaoQueFalhaNoCommit(Exception erro)
    {
        _ordens.Setup(r => r.EmTransacaoSerializadaAsync(
            It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>()))
            .Returns<Func<CancellationToken, Task>, CancellationToken>(async (f, ct) =>
            {
                await f(ct);
                throw erro;
            });
    }

    [Fact]
    public async Task Executar_FalhaNoCommit_DevePublicarEventoDeFalha()
    {
        var peca = Peca.Criar(Sku.Criar("ABC-123"), "Filtro", 25m);
        peca.RegistrarEntrada(10, "compra inicial");
        var os = OsAprovadaCom(peca, 3);

        _ordens.Setup(r => r.ObterPorIdAsync(os.Id, It.IsAny<CancellationToken>())).ReturnsAsync(os);
        _pecas.Setup(p => p.ObterPorIdAsync(peca.Id, It.IsAny<CancellationToken>())).ReturnsAsync(peca);
        TransacaoQueFalhaNoCommit(new InvalidOperationException("conflito de serializacao no commit"));

        var act = async () => await CriarUseCase().ExecutarAsync(os.Id, default);

        await act.Should().ThrowAsync<InvalidOperationException>();
        _publicador.Verify(p => p.Publicar(
            It.Is<EventoOrdemServico>(e => e.Resultado == EventoOrdemServico.ResultadoFalha)), Times.Once);
        _publicador.Verify(p => p.Publicar(
            It.Is<EventoOrdemServico>(e => e.Resultado == EventoOrdemServico.ResultadoSucesso)), Times.Never);
        _notificacoes.Verify(n => n.NotificarMudancaDeStatusAsync(
            It.IsAny<OrdemDeServico>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Executar_FalhaNoCommit_PorErroDeNegocio_NaoPublicaEvento()
    {
        var peca = Peca.Criar(Sku.Criar("ABC-123"), "Filtro", 25m);
        peca.RegistrarEntrada(10, "compra inicial");
        var os = OsAprovadaCom(peca, 3);

        _ordens.Setup(r => r.ObterPorIdAsync(os.Id, It.IsAny<CancellationToken>())).ReturnsAsync(os);
        _pecas.Setup(p => p.ObterPorIdAsync(peca.Id, It.IsAny<CancellationToken>())).ReturnsAsync(peca);
        TransacaoQueFalhaNoCommit(new OperationCanceledException());

        var act = async () => await CriarUseCase().ExecutarAsync(os.Id, default);

        await act.Should().ThrowAsync<OperationCanceledException>();
        _publicador.Verify(p => p.Publicar(It.IsAny<EventoOrdemServico>()), Times.Never);
    }
}
