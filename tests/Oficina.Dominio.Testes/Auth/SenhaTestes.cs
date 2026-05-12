using FluentAssertions;
using Oficina.Dominio.Auth;
using Xunit;

namespace Oficina.Dominio.Testes.Auth;

public class SenhaTestes
{
    [Fact]
    public void DeTextoPuro_ComSenhaValida_DeveGerarHashBCrypt()
    {
        var senha = Senha.DeTextoPuro("AlteraMe@123");

        senha.Hash.Should().NotBeNullOrWhiteSpace();
        senha.Hash.Should().StartWith("$2"); // prefixo BCrypt
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("    ")]
    public void DeTextoPuro_ComSenhaVaziaOuNula_DeveLancarExcecao(string? textoPuro)
    {
        var act = () => Senha.DeTextoPuro(textoPuro!);

        act.Should().Throw<SenhaInvalidaException>()
            .WithMessage("*não pode ser vazia*");
    }

    [Theory]
    [InlineData("12345")]
    [InlineData("abc")]
    public void DeTextoPuro_ComMenosDe8Caracteres_DeveLancarExcecao(string textoPuro)
    {
        var act = () => Senha.DeTextoPuro(textoPuro);

        act.Should().Throw<SenhaInvalidaException>()
            .WithMessage("*8 caracteres*");
    }

    [Fact]
    public void DeHashExistente_DeveCriarSemValidarFormato()
    {
        var hash = "$2a$12$abcdefghijklmnopqrstuv";

        var senha = Senha.DeHashExistente(hash);

        senha.Hash.Should().Be(hash);
    }

    [Fact]
    public void Verificar_ComTextoPuroCorreto_DeveRetornarTrue()
    {
        var senha = Senha.DeTextoPuro("AlteraMe@123");

        senha.Verificar("AlteraMe@123").Should().BeTrue();
    }

    [Fact]
    public void Verificar_ComTextoPuroIncorreto_DeveRetornarFalse()
    {
        var senha = Senha.DeTextoPuro("AlteraMe@123");

        senha.Verificar("OutraSenha@999").Should().BeFalse();
    }
}
