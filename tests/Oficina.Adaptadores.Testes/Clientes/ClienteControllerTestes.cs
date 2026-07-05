using FluentAssertions;
using Moq;
using Oficina.Adaptadores.Clientes.Controllers;
using Oficina.Aplicacao.Clientes;
using Oficina.Aplicacao.Clientes.Dtos;
using Oficina.Aplicacao.Clientes.Gateways;
using Oficina.Dominio.Clientes;

namespace Oficina.Adaptadores.Testes.Clientes;

public class ClienteControllerTestes
{
    private static ClienteController CriarController(Mock<IClienteGateway> gateway) =>
        new(
            new CriarClienteUseCase(gateway.Object),
            new ObterClientePorIdUseCase(gateway.Object),
            new BuscarClientePorDocumentoUseCase(gateway.Object),
            new ListarClientesUseCase(gateway.Object),
            new AtualizarClienteUseCase(gateway.Object),
            new RemoverClienteUseCase(gateway.Object),
            new AdicionarVeiculoUseCase(gateway.Object),
            new AtualizarVeiculoUseCase(gateway.Object),
            new RemoverVeiculoUseCase(gateway.Object),
            new ListarVeiculosUseCase(gateway.Object));

    private static Cliente CriarCliente() => Cliente.Criar("João", Documento.Criar("39053344705"),
        Email.Criar("joao@x.com"), Telefone.Criar("11987654321"));

    [Fact]
    public async Task CriarAsync_DevePersistirERetornarClienteResponseFormatado()
    {
        var gateway = new Mock<IClienteGateway>();
        gateway.Setup(g => g.ExisteDocumentoAsync(It.IsAny<Documento>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var controller = CriarController(gateway);

        var resp = await controller.CriarAsync(
            new CriarClienteRequest("João", "39053344705", "joao@x.com", "11987654321"), default);

        resp.Should().BeOfType<ClienteResponse>();
        resp.Nome.Should().Be("João");
        resp.TipoPessoa.Should().Be("PF");
        gateway.Verify(g => g.AdicionarAsync(It.IsAny<Cliente>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ObterPorIdAsync_QuandoNaoExiste_DeveRetornarNull()
    {
        var gateway = new Mock<IClienteGateway>();
        gateway.Setup(g => g.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Cliente?)null);
        var controller = CriarController(gateway);

        var resp = await controller.ObterPorIdAsync(Guid.NewGuid(), default);

        resp.Should().BeNull();
    }

    [Fact]
    public async Task ListarAsync_DeveMontarPaginaClientes()
    {
        var gateway = new Mock<IClienteGateway>();
        gateway.Setup(g => g.ListarAsync(1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { CriarCliente() });
        gateway.Setup(g => g.ContarAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        var controller = CriarController(gateway);

        var pagina = await controller.ListarAsync(1, 20, default);

        pagina.Total.Should().Be(1);
        pagina.Itens.Should().ContainSingle(c => c.Nome == "João");
    }

    [Fact]
    public async Task AdicionarVeiculoAsync_ComClienteExistente_DeveRetornarVeiculoResponse()
    {
        var gateway = new Mock<IClienteGateway>();
        var cliente = CriarCliente();
        gateway.Setup(g => g.ObterPorIdAsync(cliente.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cliente);
        var controller = CriarController(gateway);

        var resp = await controller.AdicionarVeiculoAsync(
            cliente.Id, new AdicionarVeiculoRequest("ABC1234", "Fiat", "Uno", 2020), default);

        resp.Should().NotBeNull();
        resp!.Placa.Should().Be("ABC1234");
        gateway.Verify(g => g.MarcarVeiculoComoNovo(It.IsAny<Veiculo>()), Times.Once);
    }

    [Fact]
    public async Task ListarVeiculosAsync_QuandoClienteNaoExiste_DeveRetornarNull()
    {
        var gateway = new Mock<IClienteGateway>();
        gateway.Setup(g => g.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Cliente?)null);
        var controller = CriarController(gateway);

        var resp = await controller.ListarVeiculosAsync(Guid.NewGuid(), default);

        resp.Should().BeNull();
    }
}
