using FluentAssertions;
using Oficina.Dominio.Clientes;
using Xunit;

namespace Oficina.Dominio.Testes.Clientes;

public class VeiculoTestes
{
    [Fact]
    public void Criar_ComDadosValidos_DeveInstanciar()
    {
        var v = Veiculo.Criar(
            placa: Placa.Criar("ABC1234"),
            marca: "Fiat",
            modelo: "Uno",
            ano: 2020);

        v.Id.Should().NotBeEmpty();
        v.Placa.Valor.Should().Be("ABC1234");
        v.Marca.Should().Be("Fiat");
        v.Modelo.Should().Be("Uno");
        v.Ano.Should().Be(2020);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Criar_ComMarcaVazia_DeveLancar(string? marca)
    {
        var act = () => Veiculo.Criar(Placa.Criar("ABC1234"), marca!, "Uno", 2020);
        act.Should().Throw<ArgumentException>().WithMessage("*marca*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Criar_ComModeloVazio_DeveLancar(string? modelo)
    {
        var act = () => Veiculo.Criar(Placa.Criar("ABC1234"), "Fiat", modelo!, 2020);
        act.Should().Throw<ArgumentException>().WithMessage("*modelo*");
    }

    [Theory]
    [InlineData(1899)]      // antes de 1900
    [InlineData(2100)]      // muito futuro
    public void Criar_ComAnoForaDoIntervalo_DeveLancar(int ano)
    {
        var act = () => Veiculo.Criar(Placa.Criar("ABC1234"), "Fiat", "Uno", ano);
        act.Should().Throw<ArgumentException>().WithMessage("*ano*");
    }

    [Fact]
    public void AtualizarDados_DeveAlterarMarcaModeloAno()
    {
        var v = Veiculo.Criar(Placa.Criar("ABC1234"), "Fiat", "Uno", 2010);

        v.AtualizarDados("Volkswagen", "Gol", 2015);

        v.Marca.Should().Be("Volkswagen");
        v.Modelo.Should().Be("Gol");
        v.Ano.Should().Be(2015);
        v.Placa.Valor.Should().Be("ABC1234"); // placa não muda
    }
}
