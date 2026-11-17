using FluentAssertions;
using Moq;
using Oficina.Adaptadores.OrdensServico.Controllers;
using Oficina.Aplicacao.Catalogo.Gateways;
using Oficina.Aplicacao.Clientes.Gateways;
using Oficina.Aplicacao.Estoque.Gateways;
using Oficina.Aplicacao.OrdensServico;
using Oficina.Aplicacao.OrdensServico.Dtos;
using Oficina.Aplicacao.OrdensServico.Gateways;
using Oficina.Aplicacao.OrdensServico.Telemetria;
using Oficina.Dominio.Catalogo;
using Oficina.Dominio.Clientes;
using Oficina.Dominio.OrdensServico;
using Oficina.Testes.Compartilhado;

namespace Oficina.Adaptadores.Testes.OrdensServico;

public class OrdemDeServicoControllerTestes
{
    private static OrdemDeServicoController CriarController(
        Mock<IOrdemDeServicoGateway> ordens,
        Mock<IClienteGateway> clientes,
        Mock<IServicoGateway> servicos,
        Mock<IPecaGateway> pecas)
    {
        var notificacoes = new Mock<INotificacaoGateway>();
        var publicador = new Mock<IPublicadorEventoOs>();
        return new(
            new AbrirOrdemDeServicoUseCase(clientes.Object, servicos.Object, pecas.Object, ordens.Object, publicador.Object),
            new ObterOrdemPorIdUseCase(ordens.Object),
            new ListarOrdensUseCase(ordens.Object),
            new EntregarOrdemUseCase(ordens.Object, notificacoes.Object, publicador.Object),
            new ObterTempoMedioExecucaoUseCase(ordens.Object));
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

    [Fact]
    public async Task AbrirAsync_ClienteNovo_DeveRetornarOrdemResponseFormatada()
    {
        var ordens = new Mock<IOrdemDeServicoGateway>();
        var clientes = new Mock<IClienteGateway>();
        var servicos = new Mock<IServicoGateway>();
        var pecas = new Mock<IPecaGateway>();

        var servico = Servico.Criar("Troca de óleo", "x", 150m, 30);
        clientes.Setup(c => c.ObterPorDocumentoAsync(It.IsAny<Documento>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Cliente?)null);
        servicos.Setup(s => s.ObterPorIdAsync(servico.Id, It.IsAny<CancellationToken>())).ReturnsAsync(servico);

        OrdemDeServico? adicionada = null;
        ordens.Setup(o => o.AdicionarAsync(It.IsAny<OrdemDeServico>(), It.IsAny<CancellationToken>()))
            .Callback<OrdemDeServico, CancellationToken>((o, _) => adicionada = o)
            .Returns(Task.CompletedTask);
        ordens.Setup(o => o.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => adicionada);

        var controller = CriarController(ordens, clientes, servicos, pecas);
        var req = new AbrirOrdemRequest(
            new ClienteDadosDto("39053344705", "João", "joao@x.com", "11987654321"),
            new VeiculoDadosDto("ABC1234", "Fiat", "Uno", 2020),
            new[] { new ItemServicoDto(servico.Id, 1) },
            Array.Empty<ItemPecaDto>());

        var resp = await controller.AbrirAsync(req, default);

        resp.Should().BeOfType<OrdemResponse>();
        resp.Status.Should().Be("Recebida");
        resp.ItensServico.Should().ContainSingle(i => i.Nome == "Troca de óleo");
        clientes.Verify(c => c.AdicionarAsync(It.IsAny<Cliente>(), It.IsAny<CancellationToken>()), Times.Once);
        ordens.Verify(o => o.SalvarAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EntregarAsync_OrdemFinalizada_DeveRetornarOrdemEntregue()
    {
        var ordens = new Mock<IOrdemDeServicoGateway>();
        var os = OrdemEmStatus.Criar(StatusOrdemDeServico.Finalizada);
        ordens.Setup(o => o.ObterPorIdAsync(os.Id, It.IsAny<CancellationToken>())).ReturnsAsync(os);

        var controller = CriarController(ordens, new Mock<IClienteGateway>(),
            new Mock<IServicoGateway>(), new Mock<IPecaGateway>());

        var resp = await controller.EntregarAsync(os.Id, default);

        resp.Should().NotBeNull();
        resp!.Status.Should().Be("Entregue");
        resp.EntregueEm.Should().NotBeNull();
        ordens.Verify(o => o.SalvarAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EntregarAsync_OsInexistente_DeveRetornarNull()
    {
        var ordens = new Mock<IOrdemDeServicoGateway>();
        ordens.Setup(o => o.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((OrdemDeServico?)null);
        var controller = CriarController(ordens, new Mock<IClienteGateway>(),
            new Mock<IServicoGateway>(), new Mock<IPecaGateway>());

        var resp = await controller.EntregarAsync(Guid.NewGuid(), default);

        resp.Should().BeNull();
    }
}
