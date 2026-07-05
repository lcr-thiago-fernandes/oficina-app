using FluentAssertions;
using Moq;
using Oficina.Adaptadores.Estoque.DataSources;
using Oficina.Adaptadores.Estoque.Gateways;
using Oficina.Dominio.Estoque;

namespace Oficina.Adaptadores.Testes.Estoque;

public class PecaGatewayTestes
{
    private static Peca CriarPeca() => Peca.Criar(Sku.Criar("ABC-123"), "Filtro", 25m);

    [Fact]
    public async Task ObterPorIdAsync_DeveDelegarParaODataSource()
    {
        var ds = new Mock<IPecaDataSource>();
        var esperado = CriarPeca();
        ds.Setup(d => d.ObterPorIdAsync(esperado.Id, It.IsAny<CancellationToken>())).ReturnsAsync(esperado);

        var gateway = new PecaGateway(ds.Object);
        var obtido = await gateway.ObterPorIdAsync(esperado.Id, default);

        obtido.Should().BeSameAs(esperado);
        ds.Verify(d => d.ObterPorIdAsync(esperado.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AdicionarESalvar_DevemDelegarParaODataSource()
    {
        var ds = new Mock<IPecaDataSource>();
        var gateway = new PecaGateway(ds.Object);
        var peca = CriarPeca();

        await gateway.AdicionarAsync(peca, default);
        await gateway.SalvarAsync(default);

        ds.Verify(d => d.AdicionarAsync(peca, It.IsAny<CancellationToken>()), Times.Once);
        ds.Verify(d => d.SalvarAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void MarcarMovimentacaoComoNova_DeveDelegarParaODataSource()
    {
        var ds = new Mock<IPecaDataSource>();
        var gateway = new PecaGateway(ds.Object);
        var peca = CriarPeca();
        var mov = peca.RegistrarEntrada(5, "compra");

        gateway.MarcarMovimentacaoComoNova(mov);

        ds.Verify(d => d.MarcarMovimentacaoComoNova(mov), Times.Once);
    }

    [Fact]
    public async Task EmTransacaoSerializadaAsync_DeveDelegarParaODataSource()
    {
        var ds = new Mock<IPecaDataSource>();
        Func<CancellationToken, Task> acao = _ => Task.CompletedTask;
        ds.Setup(d => d.EmTransacaoSerializadaAsync(acao, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var gateway = new PecaGateway(ds.Object);
        await gateway.EmTransacaoSerializadaAsync(acao, default);

        ds.Verify(d => d.EmTransacaoSerializadaAsync(acao, It.IsAny<CancellationToken>()), Times.Once);
    }
}
