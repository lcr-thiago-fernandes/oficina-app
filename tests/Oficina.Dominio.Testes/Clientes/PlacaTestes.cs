using FluentAssertions;
using Oficina.Dominio.Clientes;
using Xunit;

namespace Oficina.Dominio.Testes.Clientes;

public class PlacaTestes
{
    [Theory]
    [InlineData("ABC1234", "ABC1234")]      // antigo
    [InlineData("abc1234", "ABC1234")]      // normaliza upper
    [InlineData("ABC-1234", "ABC1234")]     // remove hífen
    [InlineData("ABC1D23", "ABC1D23")]      // Mercosul
    [InlineData("abc1d23", "ABC1D23")]
    public void Criar_ComFormatoValido_DeveNormalizar(string entrada, string esperado)
    {
        var p = Placa.Criar(entrada);
        p.Valor.Should().Be(esperado);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("AB1234")]      // 6 chars
    [InlineData("ABCD1234")]    // 8
    [InlineData("1234ABC")]     // ordem errada
    [InlineData("ABCDEFG")]     // sem dígitos
    [InlineData("AB!1234")]     // char especial
    public void Criar_ComFormatoInvalido_DeveLancar(string? entrada)
    {
        var act = () => Placa.Criar(entrada!);
        act.Should().Throw<PlacaInvalidaException>();
    }
}
