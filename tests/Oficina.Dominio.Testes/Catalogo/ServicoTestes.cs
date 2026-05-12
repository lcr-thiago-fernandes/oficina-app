using FluentAssertions;
using Oficina.Dominio.Catalogo;
using Xunit;

namespace Oficina.Dominio.Testes.Catalogo;

public class ServicoTestes
{
    [Fact]
    public void Criar_ComDadosValidos_DeveInstanciarAtivo()
    {
        var s = Servico.Criar("Troca de óleo", "Troca completa com filtro", 150.00m, 30);

        s.Id.Should().NotBeEmpty();
        s.Nome.Should().Be("Troca de óleo");
        s.Descricao.Should().Be("Troca completa com filtro");
        s.PrecoBase.Should().Be(150.00m);
        s.TempoEstimadoMinutos.Should().Be(30);
        s.Ativo.Should().BeTrue();
        s.CriadoEm.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Criar_ComNomeVazio_DeveLancar(string? nome)
    {
        var act = () => Servico.Criar(nome!, "desc", 10m, 30);
        act.Should().Throw<ServicoInvalidoException>().WithMessage("*nome*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-0.01)]
    [InlineData(-100)]
    public void Criar_ComPrecoNaoPositivo_DeveLancar(decimal preco)
    {
        var act = () => Servico.Criar("X", "desc", preco, 30);
        act.Should().Throw<ServicoInvalidoException>().WithMessage("*preço*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Criar_ComTempoEstimadoNaoPositivo_DeveLancar(int minutos)
    {
        var act = () => Servico.Criar("X", "desc", 10m, minutos);
        act.Should().Throw<ServicoInvalidoException>().WithMessage("*tempo*");
    }

    [Fact]
    public void AtualizarDados_DeveAlterarTodosOsCampos()
    {
        var s = Servico.Criar("Antigo", "old", 50m, 15);

        s.AtualizarDados("Novo", "new", 99.90m, 45);

        s.Nome.Should().Be("Novo");
        s.Descricao.Should().Be("new");
        s.PrecoBase.Should().Be(99.90m);
        s.TempoEstimadoMinutos.Should().Be(45);
    }

    [Fact]
    public void Inativar_DeveDefinirAtivoFalse()
    {
        var s = Servico.Criar("X", "y", 10m, 30);
        s.Inativar();
        s.Ativo.Should().BeFalse();
    }

    [Fact]
    public void Ativar_DeveDefinirAtivoTrue()
    {
        var s = Servico.Criar("X", "y", 10m, 30);
        s.Inativar();
        s.Ativar();
        s.Ativo.Should().BeTrue();
    }
}
