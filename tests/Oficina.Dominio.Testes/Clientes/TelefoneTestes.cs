using FluentAssertions;
using Oficina.Dominio.Clientes;
using Xunit;

namespace Oficina.Dominio.Testes.Clientes;

public class TelefoneTestes
{
    [Theory]
    [InlineData("(11) 98765-4321", "11987654321")]
    [InlineData("11987654321", "11987654321")]
    [InlineData("11 3344-5566", "1133445566")]
    public void Criar_ComValorValido_DevePersistirSomenteDigitos(string entrada, string esperado)
    {
        var t = Telefone.Criar(entrada);
        t.Valor.Should().Be(esperado);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("123")]                // muito curto
    [InlineData("123456789012345")]    // muito longo
    public void Criar_ComValorInvalido_DeveLancar(string? entrada)
    {
        var act = () => Telefone.Criar(entrada!);
        act.Should().Throw<ArgumentException>();
    }
}
