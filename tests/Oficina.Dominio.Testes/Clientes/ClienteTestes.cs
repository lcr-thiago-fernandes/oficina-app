using FluentAssertions;
using Oficina.Dominio.Clientes;
using Xunit;

namespace Oficina.Dominio.Testes.Clientes;

public class ClienteTestes
{
    private static Cliente NovoPF() => Cliente.Criar(
        nome: "João da Silva",
        documento: Documento.Criar("39053344705"),
        email: Email.Criar("joao@example.com"),
        telefone: Telefone.Criar("11987654321"));

    [Fact]
    public void Criar_ComDadosValidos_DeveInstanciarAtivo()
    {
        var c = NovoPF();

        c.Id.Should().NotBeEmpty();
        c.Nome.Should().Be("João da Silva");
        c.Documento.Tipo.Should().Be(TipoPessoa.PF);
        c.Email.Valor.Should().Be("joao@example.com");
        c.Ativo.Should().BeTrue();
        c.Veiculos.Should().BeEmpty();
        c.CriadoEm.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Criar_ComDocumentoPJ_DeveAceitar()
    {
        var c = Cliente.Criar(
            "Empresa Ltda",
            Documento.Criar("11444777000161"),
            Email.Criar("contato@empresa.com"),
            Telefone.Criar("1133445566"));

        c.Documento.Tipo.Should().Be(TipoPessoa.PJ);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Criar_ComNomeVazio_DeveLancar(string? nome)
    {
        var act = () => Cliente.Criar(
            nome!,
            Documento.Criar("39053344705"),
            Email.Criar("e@x.com"),
            Telefone.Criar("11987654321"));

        act.Should().Throw<ClienteInvalidoException>().WithMessage("*nome*");
    }

    [Fact]
    public void AtualizarContato_DeveAlterarEmailETelefone()
    {
        var c = NovoPF();
        c.AtualizarContato("Outro Nome", Email.Criar("novo@x.com"), Telefone.Criar("11988887777"));

        c.Nome.Should().Be("Outro Nome");
        c.Email.Valor.Should().Be("novo@x.com");
        c.Telefone.Valor.Should().Be("11988887777");
    }

    [Fact]
    public void Inativar_DeveDefinirAtivoFalse()
    {
        var c = NovoPF();
        c.Inativar();
        c.Ativo.Should().BeFalse();
    }

    [Fact]
    public void AdicionarVeiculo_DeveIncluirNaLista()
    {
        var c = NovoPF();
        var v = Veiculo.Criar(Placa.Criar("ABC1234"), "Fiat", "Uno", 2020);

        c.AdicionarVeiculo(v);

        c.Veiculos.Should().ContainSingle().Which.Placa.Valor.Should().Be("ABC1234");
    }

    [Fact]
    public void AdicionarVeiculo_ComPlacaJaExistente_DeveLancar()
    {
        var c = NovoPF();
        c.AdicionarVeiculo(Veiculo.Criar(Placa.Criar("ABC1234"), "Fiat", "Uno", 2020));

        var act = () => c.AdicionarVeiculo(
            Veiculo.Criar(Placa.Criar("ABC1234"), "VW", "Gol", 2018));

        act.Should().Throw<PlacaJaCadastradaException>();
    }

    [Fact]
    public void RemoverVeiculo_ComPlacaExistente_DeveRemover()
    {
        var c = NovoPF();
        c.AdicionarVeiculo(Veiculo.Criar(Placa.Criar("ABC1234"), "Fiat", "Uno", 2020));

        c.RemoverVeiculo(Placa.Criar("ABC1234"));

        c.Veiculos.Should().BeEmpty();
    }

    [Fact]
    public void RemoverVeiculo_ComPlacaInexistente_DeveLancar()
    {
        var c = NovoPF();
        var act = () => c.RemoverVeiculo(Placa.Criar("XYZ9999"));

        act.Should().Throw<VeiculoNaoEncontradoException>();
    }

    [Fact]
    public void AtualizarVeiculo_DeveDelegarParaEntidade()
    {
        var c = NovoPF();
        c.AdicionarVeiculo(Veiculo.Criar(Placa.Criar("ABC1234"), "Fiat", "Uno", 2010));

        c.AtualizarVeiculo(Placa.Criar("ABC1234"), "VW", "Gol", 2018);

        var v = c.Veiculos.Single();
        v.Marca.Should().Be("VW");
        v.Modelo.Should().Be("Gol");
        v.Ano.Should().Be(2018);
    }
}
