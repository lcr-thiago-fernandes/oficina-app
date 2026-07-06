using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Oficina.Infraestrutura.Persistencia;

namespace Oficina.Infraestrutura.Testes.Persistencia;

public class InicializadorBancoTestes
{
    private static IConfiguration Config(string? valor)
    {
        var dados = new Dictionary<string, string?>();
        if (valor is not null)
            dados["Bootstrap:ExecutarNoStartup"] = valor;

        return new ConfigurationBuilder()
            .AddInMemoryCollection(dados)
            .Build();
    }

    [Fact]
    public void SemAChave_Default_DeveSerTrue()
    {
        InicializadorBanco.DeveExecutarNoStartup(Config(null)).Should().BeTrue();
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("True", true)]
    [InlineData("false", false)]
    [InlineData("False", false)]
    public void ComAChave_DeveRespeitarOValor(string valor, bool esperado)
    {
        InicializadorBanco.DeveExecutarNoStartup(Config(valor)).Should().Be(esperado);
    }

    [Fact]
    public void ValorInvalido_DeveCairNoDefaultTrue()
    {
        InicializadorBanco.DeveExecutarNoStartup(Config("nao-booleano")).Should().BeTrue();
    }
}
