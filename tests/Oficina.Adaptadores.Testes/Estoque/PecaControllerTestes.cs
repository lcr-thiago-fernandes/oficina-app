using FluentAssertions;
using Moq;
using Oficina.Adaptadores.Estoque.Controllers;
using Oficina.Aplicacao.Estoque;
using Oficina.Aplicacao.Estoque.Dtos;
using Oficina.Aplicacao.Estoque.Gateways;
using Oficina.Dominio.Estoque;

namespace Oficina.Adaptadores.Testes.Estoque;

public class PecaControllerTestes
{
    private static PecaController CriarController(Mock<IPecaGateway> gateway) =>
        new(
            new CriarPecaUseCase(gateway.Object),
            new ObterPecaPorIdUseCase(gateway.Object),
            new ListarPecasUseCase(gateway.Object),
            new AtualizarPecaUseCase(gateway.Object),
            new RemoverPecaUseCase(gateway.Object),
            new RegistrarMovimentacaoUseCase(gateway.Object),
            new ListarMovimentacoesUseCase(gateway.Object));

    private static void ConfigurarTransacaoIdentidade(Mock<IPecaGateway> gateway) =>
        gateway.Setup(g => g.EmTransacaoSerializadaAsync(
                It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>()))
            .Returns<Func<CancellationToken, Task>, CancellationToken>((f, ct) => f(ct));

    [Fact]
    public async Task CriarAsync_DevePersistirERetornarPecaResponseFormatado()
    {
        var gateway = new Mock<IPecaGateway>();
        gateway.Setup(g => g.ExisteSkuAsync(It.IsAny<Sku>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var controller = CriarController(gateway);

        var resp = await controller.CriarAsync(new CriarPecaRequest("ABC-123", "Filtro", 25m), default);

        resp.Should().BeOfType<PecaResponse>();
        resp.Sku.Should().Be("ABC-123");
        resp.SaldoAtual.Should().Be(0);
        gateway.Verify(g => g.AdicionarAsync(It.IsAny<Peca>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ObterPorIdAsync_QuandoNaoExiste_DeveRetornarNull()
    {
        var gateway = new Mock<IPecaGateway>();
        gateway.Setup(g => g.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Peca?)null);
        var controller = CriarController(gateway);

        var resp = await controller.ObterPorIdAsync(Guid.NewGuid(), default);

        resp.Should().BeNull();
    }

    [Fact]
    public async Task ListarAsync_DeveMontarPaginaPecas()
    {
        var gateway = new Mock<IPecaGateway>();
        var itens = new[] { Peca.Criar(Sku.Criar("ABC-123"), "Filtro", 25m) };
        gateway.Setup(g => g.ListarAsync(null, 1, 20, false, It.IsAny<CancellationToken>())).ReturnsAsync(itens);
        gateway.Setup(g => g.ContarAsync(null, false, It.IsAny<CancellationToken>())).ReturnsAsync(1);
        var controller = CriarController(gateway);

        var pagina = await controller.ListarAsync(null, 1, 20, false, default);

        pagina.Total.Should().Be(1);
        pagina.Itens.Should().ContainSingle(p => p.Sku == "ABC-123");
    }

    [Fact]
    public async Task RegistrarMovimentacaoAsync_ComEntrada_DeveRetornarMovimentacaoResponse()
    {
        var gateway = new Mock<IPecaGateway>();
        var peca = Peca.Criar(Sku.Criar("ABC-123"), "Filtro", 25m);
        gateway.Setup(g => g.ObterPorIdAsync(peca.Id, It.IsAny<CancellationToken>())).ReturnsAsync(peca);
        ConfigurarTransacaoIdentidade(gateway);
        var controller = CriarController(gateway);

        var resp = await controller.RegistrarMovimentacaoAsync(
            peca.Id, new RegistrarMovimentacaoRequest("Entrada", 5, "Compra", null), default);

        resp.Should().NotBeNull();
        resp!.Tipo.Should().Be("Entrada");
        resp.Quantidade.Should().Be(5);
        gateway.Verify(g => g.MarcarMovimentacaoComoNova(It.IsAny<MovimentacaoEstoque>()), Times.Once);
    }

    [Fact]
    public async Task RegistrarMovimentacaoAsync_QuandoPecaNaoExiste_DeveRetornarNull()
    {
        var gateway = new Mock<IPecaGateway>();
        gateway.Setup(g => g.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Peca?)null);
        ConfigurarTransacaoIdentidade(gateway);
        var controller = CriarController(gateway);

        var resp = await controller.RegistrarMovimentacaoAsync(
            Guid.NewGuid(), new RegistrarMovimentacaoRequest("Entrada", 1, "x", null), default);

        resp.Should().BeNull();
    }

    [Fact]
    public async Task ListarMovimentacoesAsync_QuandoPecaNaoExiste_DeveRetornarNull()
    {
        var gateway = new Mock<IPecaGateway>();
        gateway.Setup(g => g.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Peca?)null);
        var controller = CriarController(gateway);

        var resp = await controller.ListarMovimentacoesAsync(Guid.NewGuid(), default);

        resp.Should().BeNull();
    }
}
