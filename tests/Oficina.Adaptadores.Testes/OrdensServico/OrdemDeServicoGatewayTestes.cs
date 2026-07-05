using FluentAssertions;
using Moq;
using Oficina.Adaptadores.OrdensServico.DataSources;
using Oficina.Adaptadores.OrdensServico.Gateways;
using Oficina.Aplicacao.OrdensServico.Gateways;
using Oficina.Dominio.OrdensServico;

namespace Oficina.Adaptadores.Testes.OrdensServico;

public class OrdemDeServicoGatewayTestes
{
    private static OrdemDeServico CriarOrdem() =>
        OrdemDeServico.Criar(Guid.NewGuid(), Guid.NewGuid());

    [Fact]
    public async Task ObterPorIdAsync_DeveDelegarParaODataSource()
    {
        var ds = new Mock<IOrdemDeServicoDataSource>();
        var esperada = CriarOrdem();
        ds.Setup(d => d.ObterPorIdAsync(esperada.Id, It.IsAny<CancellationToken>())).ReturnsAsync(esperada);

        var gateway = new OrdemDeServicoGateway(ds.Object);
        var obtida = await gateway.ObterPorIdAsync(esperada.Id, default);

        obtida.Should().BeSameAs(esperada);
        ds.Verify(d => d.ObterPorIdAsync(esperada.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ObterPorNumeroAsync_DeveDelegarParaODataSource()
    {
        var ds = new Mock<IOrdemDeServicoDataSource>();
        var esperada = CriarOrdem();
        ds.Setup(d => d.ObterPorNumeroAsync(42, It.IsAny<CancellationToken>())).ReturnsAsync(esperada);

        var gateway = new OrdemDeServicoGateway(ds.Object);
        var obtida = await gateway.ObterPorNumeroAsync(42, default);

        obtida.Should().BeSameAs(esperada);
        ds.Verify(d => d.ObterPorNumeroAsync(42, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AdicionarESalvar_DevemDelegarParaODataSource()
    {
        var ds = new Mock<IOrdemDeServicoDataSource>();
        var gateway = new OrdemDeServicoGateway(ds.Object);
        var ordem = CriarOrdem();

        await gateway.AdicionarAsync(ordem, default);
        await gateway.SalvarAsync(default);

        ds.Verify(d => d.AdicionarAsync(ordem, It.IsAny<CancellationToken>()), Times.Once);
        ds.Verify(d => d.SalvarAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ListarEContar_DevemDelegarParaODataSource()
    {
        var ds = new Mock<IOrdemDeServicoDataSource>();
        var lista = new[] { CriarOrdem() };
        ds.Setup(d => d.ListarAsync(null, 1, 20, It.IsAny<CancellationToken>())).ReturnsAsync(lista);
        ds.Setup(d => d.ContarAsync(null, It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var gateway = new OrdemDeServicoGateway(ds.Object);
        var itens = await gateway.ListarAsync(null, 1, 20, default);
        var total = await gateway.ContarAsync(null, default);

        itens.Should().BeSameAs(lista);
        total.Should().Be(1);
    }

    [Fact]
    public void MarcarItensComoNovos_DevemDelegarParaODataSource()
    {
        var ds = new Mock<IOrdemDeServicoDataSource>();
        var gateway = new OrdemDeServicoGateway(ds.Object);
        var ordem = CriarOrdem();
        var itemServico = ordem.AdicionarItemServico(Guid.NewGuid(), "S", 10m, 1);
        var itemPeca = ordem.AdicionarItemPeca(Guid.NewGuid(), "P", 5m, 1);

        gateway.MarcarItemServicoComoNovo(itemServico);
        gateway.MarcarItemPecaComoNovo(itemPeca);

        ds.Verify(d => d.MarcarItemServicoComoNovo(itemServico), Times.Once);
        ds.Verify(d => d.MarcarItemPecaComoNovo(itemPeca), Times.Once);
    }

    [Fact]
    public async Task EmTransacaoSerializadaAsync_DeveDelegarParaODataSource()
    {
        var ds = new Mock<IOrdemDeServicoDataSource>();
        Func<CancellationToken, Task> acao = _ => Task.CompletedTask;
        ds.Setup(d => d.EmTransacaoSerializadaAsync(acao, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var gateway = new OrdemDeServicoGateway(ds.Object);
        await gateway.EmTransacaoSerializadaAsync(acao, default);

        ds.Verify(d => d.EmTransacaoSerializadaAsync(acao, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ObterTempoMedioExecucaoAsync_DeveDelegarParaODataSource()
    {
        var ds = new Mock<IOrdemDeServicoDataSource>();
        var metrica = new MetricaTempoMedio(2, TimeSpan.FromMinutes(60), TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(90));
        ds.Setup(d => d.ObterTempoMedioExecucaoAsync(It.IsAny<CancellationToken>())).ReturnsAsync(metrica);

        var gateway = new OrdemDeServicoGateway(ds.Object);
        var obtida = await gateway.ObterTempoMedioExecucaoAsync(default);

        obtida.Should().BeSameAs(metrica);
        ds.Verify(d => d.ObterTempoMedioExecucaoAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
