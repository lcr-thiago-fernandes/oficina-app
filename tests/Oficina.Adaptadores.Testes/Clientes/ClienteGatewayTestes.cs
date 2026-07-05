using FluentAssertions;
using Moq;
using Oficina.Adaptadores.Clientes.DataSources;
using Oficina.Adaptadores.Clientes.Gateways;
using Oficina.Dominio.Clientes;

namespace Oficina.Adaptadores.Testes.Clientes;

public class ClienteGatewayTestes
{
    private static Cliente CriarCliente() => Cliente.Criar("João", Documento.Criar("39053344705"),
        Email.Criar("joao@x.com"), Telefone.Criar("11987654321"));

    [Fact]
    public async Task ObterPorIdAsync_DeveDelegarParaODataSource()
    {
        var ds = new Mock<IClienteDataSource>();
        var esperado = CriarCliente();
        ds.Setup(d => d.ObterPorIdAsync(esperado.Id, It.IsAny<CancellationToken>())).ReturnsAsync(esperado);

        var gateway = new ClienteGateway(ds.Object);
        var obtido = await gateway.ObterPorIdAsync(esperado.Id, default);

        obtido.Should().BeSameAs(esperado);
        ds.Verify(d => d.ObterPorIdAsync(esperado.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AdicionarESalvar_DevemDelegarParaODataSource()
    {
        var ds = new Mock<IClienteDataSource>();
        var gateway = new ClienteGateway(ds.Object);
        var cliente = CriarCliente();

        await gateway.AdicionarAsync(cliente, default);
        await gateway.SalvarAsync(default);

        ds.Verify(d => d.AdicionarAsync(cliente, It.IsAny<CancellationToken>()), Times.Once);
        ds.Verify(d => d.SalvarAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void RemoverEMarcarVeiculoComoNovo_DevemDelegarParaODataSource()
    {
        var ds = new Mock<IClienteDataSource>();
        var gateway = new ClienteGateway(ds.Object);
        var cliente = CriarCliente();
        var veiculo = Veiculo.Criar(Placa.Criar("ABC1234"), "Fiat", "Uno", 2020);

        gateway.Remover(cliente);
        gateway.MarcarVeiculoComoNovo(veiculo);

        ds.Verify(d => d.Remover(cliente), Times.Once);
        ds.Verify(d => d.MarcarVeiculoComoNovo(veiculo), Times.Once);
    }
}
