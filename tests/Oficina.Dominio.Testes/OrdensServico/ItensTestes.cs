using FluentAssertions;
using Oficina.Dominio.OrdensServico;
using Xunit;

namespace Oficina.Dominio.Testes.OrdensServico;

public class ItensTestes
{
    [Fact]
    public void ItemServico_Criar_DeveCalcularSubtotal()
    {
        var item = ItemServico.Criar(
            servicoId: Guid.NewGuid(),
            servicoNome: "Troca de óleo",
            precoSnapshot: 150m,
            quantidade: 2);

        item.Subtotal.Should().Be(300m);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ItemServico_ComQuantidadeNaoPositiva_DeveLancar(int qtd)
    {
        var act = () => ItemServico.Criar(Guid.NewGuid(), "x", 10m, qtd);
        act.Should().Throw<ItemInvalidoException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-0.01)]
    public void ItemServico_ComPrecoNaoPositivo_DeveLancar(decimal preco)
    {
        var act = () => ItemServico.Criar(Guid.NewGuid(), "x", preco, 1);
        act.Should().Throw<ItemInvalidoException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void ItemServico_ComNomeVazio_DeveLancar(string? nome)
    {
        var act = () => ItemServico.Criar(Guid.NewGuid(), nome!, 10m, 1);
        act.Should().Throw<ItemInvalidoException>();
    }

    [Fact]
    public void ItemPeca_Criar_DeveCalcularSubtotal()
    {
        var item = ItemPeca.Criar(Guid.NewGuid(), "Filtro de óleo", 25m, 4);
        item.Subtotal.Should().Be(100m);
    }
}
