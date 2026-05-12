using FluentAssertions;
using Oficina.Dominio.Auth;
using Xunit;

namespace Oficina.Dominio.Testes.Auth;

public class UsernameTestes
{
    [Theory]
    [InlineData("admin")]
    [InlineData("joao.silva")]
    [InlineData("user_42")]
    public void Criar_ComValorValido_DeveNormalizarParaMinusculo(string entrada)
    {
        var username = Username.Criar(entrada.ToUpper());
        username.Valor.Should().Be(entrada.ToLower());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_ComValorVazio_DeveLancar(string? entrada)
    {
        var act = () => Username.Criar(entrada!);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Username*");
    }

    [Theory]
    [InlineData("ab")]      // < 3
    [InlineData("a")]
    public void Criar_ComMenosDe3Caracteres_DeveLancar(string entrada)
    {
        var act = () => Username.Criar(entrada);
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("user@name")]
    [InlineData("user name")]
    [InlineData("user!")]
    public void Criar_ComCaracteresInvalidos_DeveLancar(string entrada)
    {
        var act = () => Username.Criar(entrada);
        act.Should().Throw<ArgumentException>();
    }
}
