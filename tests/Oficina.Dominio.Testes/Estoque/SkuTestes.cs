using FluentAssertions;
using Oficina.Dominio.Estoque;
using Xunit;

namespace Oficina.Dominio.Testes.Estoque;

public class SkuTestes
{
    [Theory]
    [InlineData("ABC-123", "ABC-123")]
    [InlineData("abc-123", "ABC-123")]
    [InlineData("  ABC-123  ", "ABC-123")]
    public void Criar_ComValorValido_DeveNormalizar(string entrada, string esperado)
    {
        var sku = Sku.Criar(entrada);
        sku.Valor.Should().Be(esperado);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Criar_ComVazio_DeveLancar(string? entrada)
    {
        var act = () => Sku.Criar(entrada!);
        act.Should().Throw<SkuInvalidoException>();
    }

    [Theory]
    [InlineData("AB")]                                  // < 3
    [InlineData("0123456789012345678901234567890123456789012345678901")]  // > 50
    public void Criar_ComTamanhoForaDoIntervalo_DeveLancar(string entrada)
    {
        var act = () => Sku.Criar(entrada);
        act.Should().Throw<SkuInvalidoException>();
    }
}
