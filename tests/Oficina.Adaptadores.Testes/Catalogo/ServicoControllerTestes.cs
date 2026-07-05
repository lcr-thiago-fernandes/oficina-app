using FluentAssertions;
using Moq;
using Oficina.Adaptadores.Catalogo.Controllers;
using Oficina.Aplicacao.Catalogo;
using Oficina.Aplicacao.Catalogo.Dtos;
using Oficina.Aplicacao.Catalogo.Gateways;
using Oficina.Dominio.Catalogo;

namespace Oficina.Adaptadores.Testes.Catalogo;

public class ServicoControllerTestes
{
    private static ServicoController CriarController(Mock<IServicoGateway> gateway) =>
        new(
            new CriarServicoUseCase(gateway.Object),
            new ObterServicoPorIdUseCase(gateway.Object),
            new ListarServicosUseCase(gateway.Object),
            new AtualizarServicoUseCase(gateway.Object),
            new RemoverServicoUseCase(gateway.Object));

    [Fact]
    public async Task CriarAsync_DevePersistirERetornarServicoResponseFormatado()
    {
        var gateway = new Mock<IServicoGateway>();
        var controller = CriarController(gateway);

        var resp = await controller.CriarAsync(new CriarServicoRequest("Troca de óleo", "d", 150m, 30), default);

        resp.Should().BeOfType<ServicoResponse>();
        resp.Nome.Should().Be("Troca de óleo");
        resp.PrecoBase.Should().Be(150m);
        gateway.Verify(g => g.AdicionarAsync(It.IsAny<Servico>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ObterPorIdAsync_QuandoNaoExiste_DeveRetornarNull()
    {
        var gateway = new Mock<IServicoGateway>();
        gateway.Setup(g => g.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Servico?)null);
        var controller = CriarController(gateway);

        var resp = await controller.ObterPorIdAsync(Guid.NewGuid(), default);

        resp.Should().BeNull();
    }

    [Fact]
    public async Task ListarAsync_DeveMontarPaginaServicos()
    {
        var gateway = new Mock<IServicoGateway>();
        var itens = new[] { Servico.Criar("A", "x", 10m, 5) };
        gateway.Setup(g => g.ListarAsync(null, 1, 20, false, It.IsAny<CancellationToken>())).ReturnsAsync(itens);
        gateway.Setup(g => g.ContarAsync(null, false, It.IsAny<CancellationToken>())).ReturnsAsync(1);
        var controller = CriarController(gateway);

        var pagina = await controller.ListarAsync(null, 1, 20, false, default);

        pagina.Total.Should().Be(1);
        pagina.Itens.Should().ContainSingle(i => i.Nome == "A");
    }
}
