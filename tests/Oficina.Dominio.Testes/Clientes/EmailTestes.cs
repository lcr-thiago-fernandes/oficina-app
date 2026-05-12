using FluentAssertions;
using Oficina.Dominio.Clientes;
using Xunit;

namespace Oficina.Dominio.Testes.Clientes;

public class EmailTestes
{
    [Theory]
    [InlineData("user@example.com")]
    [InlineData("first.last+tag@sub.domain.com")]
    public void Criar_ComValorValido_DeveNormalizarMinusculo(string entrada)
    {
        var e = Email.Criar(entrada.ToUpper());
        e.Valor.Should().Be(entrada.ToLower());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("sem-arroba.com")]
    [InlineData("@semuser.com")]
    [InlineData("user@")]
    [InlineData("user@no-tld")]
    public void Criar_ComValorInvalido_DeveLancar(string? entrada)
    {
        var act = () => Email.Criar(entrada!);
        act.Should().Throw<EmailInvalidoException>();
    }
}
