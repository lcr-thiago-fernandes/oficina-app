using FluentAssertions;
using Moq;
using Oficina.Adaptadores.Catalogo.DataSources;
using Oficina.Adaptadores.Catalogo.Gateways;
using Oficina.Dominio.Catalogo;

namespace Oficina.Adaptadores.Testes.Catalogo;

public class ServicoGatewayTestes
{
    [Fact]
    public async Task ObterPorIdAsync_DeveDelegarParaODataSource()
    {
        var ds = new Mock<IServicoDataSource>();
        var esperado = Servico.Criar("A", "x", 10m, 5);
        ds.Setup(d => d.ObterPorIdAsync(esperado.Id, It.IsAny<CancellationToken>())).ReturnsAsync(esperado);

        var gateway = new ServicoGateway(ds.Object);
        var obtido = await gateway.ObterPorIdAsync(esperado.Id, default);

        obtido.Should().BeSameAs(esperado);
        ds.Verify(d => d.ObterPorIdAsync(esperado.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AdicionarESalvar_DevemDelegarParaODataSource()
    {
        var ds = new Mock<IServicoDataSource>();
        var gateway = new ServicoGateway(ds.Object);
        var servico = Servico.Criar("A", "x", 10m, 5);

        await gateway.AdicionarAsync(servico, default);
        await gateway.SalvarAsync(default);

        ds.Verify(d => d.AdicionarAsync(servico, It.IsAny<CancellationToken>()), Times.Once);
        ds.Verify(d => d.SalvarAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
