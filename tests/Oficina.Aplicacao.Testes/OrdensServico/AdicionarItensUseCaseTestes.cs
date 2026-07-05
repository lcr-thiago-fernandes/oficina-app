using FluentAssertions;
using Moq;
using Oficina.Aplicacao.Catalogo.Gateways;
using Oficina.Aplicacao.Estoque.Gateways;
using Oficina.Aplicacao.OrdensServico;
using Oficina.Aplicacao.OrdensServico.Dtos;
using Oficina.Aplicacao.OrdensServico.Gateways;
using Oficina.Dominio.Catalogo;
using Oficina.Dominio.Estoque;
using Oficina.Dominio.OrdensServico;
using Xunit;

namespace Oficina.Aplicacao.Testes.OrdensServico;

public class AdicionarItensUseCaseTestes
{
    [Fact]
    public async Task AdicionarItemServico_DeveTirarSnapshotDoNomeEPreco()
    {
        var ordem = OrdemDeServico.Criar(Guid.NewGuid(), Guid.NewGuid());
        var serv = Servico.Criar("Troca de óleo", "x", 150m, 30);

        var ordens = new Mock<IOrdemDeServicoGateway>();
        ordens.Setup(r => r.ObterPorIdAsync(ordem.Id, It.IsAny<CancellationToken>())).ReturnsAsync(ordem);
        var servicos = new Mock<IServicoGateway>();
        servicos.Setup(s => s.ObterPorIdAsync(serv.Id, It.IsAny<CancellationToken>())).ReturnsAsync(serv);

        var item = await new AdicionarItemServicoUseCase(ordens.Object, servicos.Object)
            .ExecutarAsync(ordem.Id, new AdicionarItemServicoRequest(serv.Id, 2), default);

        item.Should().NotBeNull();
        item!.ServicoNome.Should().Be("Troca de óleo");
        item.PrecoSnapshot.Should().Be(150m);
        item.Subtotal.Should().Be(300m);
    }

    [Fact]
    public async Task AdicionarItemPeca_DeveTirarSnapshotDoNomeEPreco()
    {
        var ordem = OrdemDeServico.Criar(Guid.NewGuid(), Guid.NewGuid());
        var peca = Peca.Criar(Sku.Criar("ABC-123"), "Filtro", 25m);

        var ordens = new Mock<IOrdemDeServicoGateway>();
        ordens.Setup(r => r.ObterPorIdAsync(ordem.Id, It.IsAny<CancellationToken>())).ReturnsAsync(ordem);
        var pecas = new Mock<IPecaGateway>();
        pecas.Setup(p => p.ObterPorIdAsync(peca.Id, It.IsAny<CancellationToken>())).ReturnsAsync(peca);

        var item = await new AdicionarItemPecaUseCase(ordens.Object, pecas.Object)
            .ExecutarAsync(ordem.Id, new AdicionarItemPecaRequest(peca.Id, 4), default);

        item.Should().NotBeNull();
        item!.PecaNome.Should().Be("Filtro");
        item.PrecoSnapshot.Should().Be(25m);
        item.Subtotal.Should().Be(100m);
    }

    [Fact]
    public async Task AdicionarItemServico_ServicoInativo_DeveLancar()
    {
        var ordem = OrdemDeServico.Criar(Guid.NewGuid(), Guid.NewGuid());
        var serv = Servico.Criar("S", "x", 10m, 30);
        serv.Inativar();

        var ordens = new Mock<IOrdemDeServicoGateway>();
        ordens.Setup(r => r.ObterPorIdAsync(ordem.Id, It.IsAny<CancellationToken>())).ReturnsAsync(ordem);
        var servicos = new Mock<IServicoGateway>();
        servicos.Setup(s => s.ObterPorIdAsync(serv.Id, It.IsAny<CancellationToken>())).ReturnsAsync(serv);

        var act = async () => await new AdicionarItemServicoUseCase(ordens.Object, servicos.Object)
            .ExecutarAsync(ordem.Id, new AdicionarItemServicoRequest(serv.Id, 1), default);

        await act.Should().ThrowAsync<OrdemInvalidaException>().WithMessage("*inativo*");
    }
}
