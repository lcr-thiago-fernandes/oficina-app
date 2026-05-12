using FluentAssertions;
using Moq;
using Oficina.Aplicacao.OrdensServico;
using Oficina.Aplicacao.OrdensServico.Dtos;
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

        var ordens = new Mock<IOrdemDeServicoRepositorio>();
        ordens.Setup(r => r.ObterPorIdAsync(ordem.Id, It.IsAny<CancellationToken>())).ReturnsAsync(ordem);
        var servicos = new Mock<IServicoRepositorio>();
        servicos.Setup(s => s.ObterPorIdAsync(serv.Id, It.IsAny<CancellationToken>())).ReturnsAsync(serv);

        var resp = await new AdicionarItemServicoUseCase(ordens.Object, servicos.Object)
            .ExecutarAsync(ordem.Id, new AdicionarItemServicoRequest(serv.Id, 2), default);

        resp.Should().NotBeNull();
        resp!.Nome.Should().Be("Troca de óleo");
        resp.PrecoUnitario.Should().Be(150m);
        resp.Subtotal.Should().Be(300m);
    }

    [Fact]
    public async Task AdicionarItemPeca_DeveTirarSnapshotDoNomeEPreco()
    {
        var ordem = OrdemDeServico.Criar(Guid.NewGuid(), Guid.NewGuid());
        var peca = Peca.Criar(Sku.Criar("ABC-123"), "Filtro", 25m);

        var ordens = new Mock<IOrdemDeServicoRepositorio>();
        ordens.Setup(r => r.ObterPorIdAsync(ordem.Id, It.IsAny<CancellationToken>())).ReturnsAsync(ordem);
        var pecas = new Mock<IPecaRepositorio>();
        pecas.Setup(p => p.ObterPorIdAsync(peca.Id, It.IsAny<CancellationToken>())).ReturnsAsync(peca);

        var resp = await new AdicionarItemPecaUseCase(ordens.Object, pecas.Object)
            .ExecutarAsync(ordem.Id, new AdicionarItemPecaRequest(peca.Id, 4), default);

        resp.Should().NotBeNull();
        resp!.Nome.Should().Be("Filtro");
        resp.PrecoUnitario.Should().Be(25m);
        resp.Subtotal.Should().Be(100m);
    }

    [Fact]
    public async Task AdicionarItemServico_ServicoInativo_DeveLancar()
    {
        var ordem = OrdemDeServico.Criar(Guid.NewGuid(), Guid.NewGuid());
        var serv = Servico.Criar("S", "x", 10m, 30);
        serv.Inativar();

        var ordens = new Mock<IOrdemDeServicoRepositorio>();
        ordens.Setup(r => r.ObterPorIdAsync(ordem.Id, It.IsAny<CancellationToken>())).ReturnsAsync(ordem);
        var servicos = new Mock<IServicoRepositorio>();
        servicos.Setup(s => s.ObterPorIdAsync(serv.Id, It.IsAny<CancellationToken>())).ReturnsAsync(serv);

        var act = async () => await new AdicionarItemServicoUseCase(ordens.Object, servicos.Object)
            .ExecutarAsync(ordem.Id, new AdicionarItemServicoRequest(serv.Id, 1), default);

        await act.Should().ThrowAsync<OrdemInvalidaException>().WithMessage("*inativo*");
    }
}
