using FluentAssertions;
using Moq;
using Oficina.Adaptadores.OrdensServico.Controllers;
using Oficina.Aplicacao.Catalogo.Gateways;
using Oficina.Aplicacao.Clientes.Gateways;
using Oficina.Aplicacao.Estoque.Gateways;
using Oficina.Aplicacao.OrdensServico;
using Oficina.Aplicacao.OrdensServico.Dtos;
using Oficina.Aplicacao.OrdensServico.Gateways;
using Oficina.Dominio.Catalogo;
using Oficina.Dominio.Clientes;
using Oficina.Dominio.OrdensServico;

namespace Oficina.Adaptadores.Testes.OrdensServico;

public class OrdemDeServicoControllerTestes
{
    private static OrdemDeServicoController CriarController(
        Mock<IOrdemDeServicoGateway> ordens,
        Mock<IClienteGateway> clientes,
        Mock<IServicoGateway> servicos,
        Mock<IPecaGateway> pecas) =>
        new(
            new CriarOrdemUseCase(ordens.Object, clientes.Object),
            new ObterOrdemPorIdUseCase(ordens.Object),
            new ListarOrdensUseCase(ordens.Object),
            new IniciarDiagnosticoUseCase(ordens.Object),
            new EnviarOrcamentoParaAprovacaoUseCase(ordens.Object),
            new IniciarExecucaoUseCase(ordens.Object, pecas.Object),
            new FinalizarOrdemUseCase(ordens.Object),
            new EntregarOrdemUseCase(ordens.Object),
            new AdicionarItemServicoUseCase(ordens.Object, servicos.Object),
            new RemoverItemServicoUseCase(ordens.Object),
            new AdicionarItemPecaUseCase(ordens.Object, pecas.Object),
            new RemoverItemPecaUseCase(ordens.Object),
            new ObterTempoMedioExecucaoUseCase(ordens.Object));

    [Fact]
    public async Task CriarAsync_DeveRetornarOrdemResponseFormatada()
    {
        var ordens = new Mock<IOrdemDeServicoGateway>();
        var clientes = new Mock<IClienteGateway>();
        var servicos = new Mock<IServicoGateway>();
        var pecas = new Mock<IPecaGateway>();

        var cliente = Cliente.Criar("J", Documento.Criar("39053344705"),
            Email.Criar("e@x.com"), Telefone.Criar("11987654321"));
        var veiculo = Veiculo.Criar(Placa.Criar("ABC1234"), "F", "U", 2020);
        cliente.AdicionarVeiculo(veiculo);
        clientes.Setup(c => c.ObterPorIdAsync(cliente.Id, It.IsAny<CancellationToken>())).ReturnsAsync(cliente);
        ordens.Setup(o => o.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OrdemDeServico.Criar(cliente.Id, veiculo.Id));

        var controller = CriarController(ordens, clientes, servicos, pecas);
        var resp = await controller.CriarAsync(new CriarOrdemRequest(cliente.Id, veiculo.Id, null), default);

        resp.Should().BeOfType<OrdemResponse>();
        resp.Status.Should().Be("Recebida");
        ordens.Verify(o => o.AdicionarAsync(It.IsAny<OrdemDeServico>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ObterPorIdAsync_QuandoNaoExiste_DeveRetornarNull()
    {
        var ordens = new Mock<IOrdemDeServicoGateway>();
        ordens.Setup(o => o.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((OrdemDeServico?)null);
        var controller = CriarController(ordens, new Mock<IClienteGateway>(),
            new Mock<IServicoGateway>(), new Mock<IPecaGateway>());

        var resp = await controller.ObterPorIdAsync(Guid.NewGuid(), default);

        resp.Should().BeNull();
    }

    [Fact]
    public async Task ListarAsync_DeveMontarPaginaOrdens()
    {
        var ordens = new Mock<IOrdemDeServicoGateway>();
        var lista = new[] { OrdemDeServico.Criar(Guid.NewGuid(), Guid.NewGuid()) };
        ordens.Setup(o => o.ListarAsync(null, 1, 20, It.IsAny<CancellationToken>())).ReturnsAsync(lista);
        ordens.Setup(o => o.ContarAsync(null, It.IsAny<CancellationToken>())).ReturnsAsync(1);
        var controller = CriarController(ordens, new Mock<IClienteGateway>(),
            new Mock<IServicoGateway>(), new Mock<IPecaGateway>());

        var pagina = await controller.ListarAsync(null, 1, 20, default);

        pagina.Total.Should().Be(1);
        pagina.Itens.Should().ContainSingle(o => o.Status == "Recebida");
    }

    [Fact]
    public async Task AdicionarItemServicoAsync_DeveRetornarItemServicoResponse()
    {
        var ordens = new Mock<IOrdemDeServicoGateway>();
        var servicos = new Mock<IServicoGateway>();
        var ordem = OrdemDeServico.Criar(Guid.NewGuid(), Guid.NewGuid());
        var serv = Servico.Criar("Troca de óleo", "x", 150m, 30);
        ordens.Setup(o => o.ObterPorIdAsync(ordem.Id, It.IsAny<CancellationToken>())).ReturnsAsync(ordem);
        servicos.Setup(s => s.ObterPorIdAsync(serv.Id, It.IsAny<CancellationToken>())).ReturnsAsync(serv);
        var controller = CriarController(ordens, new Mock<IClienteGateway>(), servicos, new Mock<IPecaGateway>());

        var resp = await controller.AdicionarItemServicoAsync(
            ordem.Id, new AdicionarItemServicoRequest(serv.Id, 2), default);

        resp.Should().NotBeNull();
        resp!.Nome.Should().Be("Troca de óleo");
        resp.Subtotal.Should().Be(300m);
        ordens.Verify(o => o.MarcarItemServicoComoNovo(It.IsAny<ItemServico>()), Times.Once);
    }

    [Fact]
    public async Task ObterTempoMedioExecucaoAsync_DeveConverterParaMinutos()
    {
        var ordens = new Mock<IOrdemDeServicoGateway>();
        ordens.Setup(o => o.ObterTempoMedioExecucaoAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MetricaTempoMedio(2, TimeSpan.FromMinutes(60), TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(90)));
        var controller = CriarController(ordens, new Mock<IClienteGateway>(),
            new Mock<IServicoGateway>(), new Mock<IPecaGateway>());

        var resp = await controller.ObterTempoMedioExecucaoAsync(default);

        resp.TotalOrdensConcluidas.Should().Be(2);
        resp.TempoMedioMinutos.Should().Be(60);
        resp.TempoMinimoMinutos.Should().Be(30);
        resp.TempoMaximoMinutos.Should().Be(90);
    }
}
