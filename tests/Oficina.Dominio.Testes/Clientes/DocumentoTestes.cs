using FluentAssertions;
using Oficina.Dominio.Clientes;
using Xunit;

namespace Oficina.Dominio.Testes.Clientes;

public class DocumentoTestes
{
    [Theory]
    [InlineData("39053344705")]    // CPF válido
    [InlineData("390.533.447-05")] // mesmo, com máscara
    public void CriarCpf_ComValorValido_DevePersistirSomenteDigitos(string entrada)
    {
        var doc = Documento.Criar(entrada);
        doc.Valor.Should().Be("39053344705");
        doc.Tipo.Should().Be(TipoPessoa.PF);
    }

    [Theory]
    [InlineData("11144477735")]
    [InlineData("12345678909")]
    public void CriarCpf_ComOutrosCpfsValidos_DeveAceitar(string entrada)
    {
        var doc = Documento.Criar(entrada);
        doc.Tipo.Should().Be(TipoPessoa.PF);
    }

    [Theory]
    [InlineData("11111111111")]    // todos iguais
    [InlineData("12345678900")]    // dígitos errados
    [InlineData("00000000000")]
    public void CriarCpf_ComCpfInvalido_DeveLancar(string entrada)
    {
        var act = () => Documento.Criar(entrada);
        act.Should().Throw<DocumentoInvalidoException>();
    }

    [Theory]
    [InlineData("11444777000161")]
    [InlineData("11.444.777/0001-61")]
    public void CriarCnpj_ComValorValido_DevePersistirSomenteDigitos(string entrada)
    {
        var doc = Documento.Criar(entrada);
        doc.Valor.Should().Be("11444777000161");
        doc.Tipo.Should().Be(TipoPessoa.PJ);
    }

    [Theory]
    [InlineData("11111111111111")]
    [InlineData("00000000000000")]
    [InlineData("11444777000160")]   // dígito errado
    public void CriarCnpj_ComCnpjInvalido_DeveLancar(string entrada)
    {
        var act = () => Documento.Criar(entrada);
        act.Should().Throw<DocumentoInvalidoException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_ComVazio_DeveLancar(string? entrada)
    {
        var act = () => Documento.Criar(entrada!);
        act.Should().Throw<DocumentoInvalidoException>();
    }

    [Theory]
    [InlineData("12345")]
    [InlineData("123456789012345")]
    public void Criar_ComTamanhoInvalido_DeveLancar(string entrada)
    {
        var act = () => Documento.Criar(entrada);
        act.Should().Throw<DocumentoInvalidoException>();
    }

    [Fact]
    public void Mascarado_ParaCpf_DeveRetornarFormatoVisual()
    {
        var doc = Documento.Criar("39053344705");
        doc.Mascarado().Should().Be("390.533.447-05");
    }

    [Fact]
    public void Mascarado_ParaCnpj_DeveRetornarFormatoVisual()
    {
        var doc = Documento.Criar("11444777000161");
        doc.Mascarado().Should().Be("11.444.777/0001-61");
    }
}
