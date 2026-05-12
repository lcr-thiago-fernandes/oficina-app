using FluentAssertions;
using Oficina.Dominio.Estoque;
using Xunit;

namespace Oficina.Dominio.Testes.Estoque;

public class PecaTestes
{
    private static Peca Nova(decimal preco = 50m) =>
        Peca.Criar(Sku.Criar("ABC-123"), "Filtro de óleo", preco);

    [Fact]
    public void Criar_ComDadosValidos_DeveTerSaldoZeroEAtiva()
    {
        var p = Nova();
        p.Id.Should().NotBeEmpty();
        p.Sku.Valor.Should().Be("ABC-123");
        p.Nome.Should().Be("Filtro de óleo");
        p.PrecoUnitario.Should().Be(50m);
        p.SaldoAtual.Should().Be(0);
        p.Ativo.Should().BeTrue();
        p.Movimentacoes.Should().BeEmpty();
    }

    [Fact]
    public void Criar_ComPrecoZero_DeveLancar()
    {
        var act = () => Peca.Criar(Sku.Criar("ABC-123"), "x", 0m);
        act.Should().Throw<PecaInvalidaException>();
    }

    [Fact]
    public void Criar_ComNomeVazio_DeveLancar()
    {
        var act = () => Peca.Criar(Sku.Criar("ABC-123"), "", 10m);
        act.Should().Throw<PecaInvalidaException>();
    }

    [Fact]
    public void RegistrarEntrada_DeveAumentarSaldoEAdicionarMovimentacao()
    {
        var p = Nova();
        p.RegistrarEntrada(10, "Compra inicial");

        p.SaldoAtual.Should().Be(10);
        p.Movimentacoes.Should().ContainSingle()
            .Which.Tipo.Should().Be(TipoMovimentacao.Entrada);
    }

    [Fact]
    public void RegistrarEntrada_VariasVezes_DeveAcumular()
    {
        var p = Nova();
        p.RegistrarEntrada(5, "x");
        p.RegistrarEntrada(7, "y");

        p.SaldoAtual.Should().Be(12);
        p.Movimentacoes.Should().HaveCount(2);
    }

    [Fact]
    public void RegistrarSaida_ComSaldoSuficiente_DeveDiminuir()
    {
        var p = Nova();
        p.RegistrarEntrada(10, "compra");
        p.RegistrarSaida(3, "OS#1", ordemServicoId: Guid.NewGuid());

        p.SaldoAtual.Should().Be(7);
        var saida = p.Movimentacoes.Last();
        saida.Tipo.Should().Be(TipoMovimentacao.Saida);
        saida.OrdemServicoId.Should().NotBeNull();
    }

    [Fact]
    public void RegistrarSaida_ComSaldoInsuficiente_DeveLancarSemAlterarSaldo()
    {
        var p = Nova();
        p.RegistrarEntrada(2, "compra");

        var act = () => p.RegistrarSaida(5, "tentativa", null);

        act.Should().Throw<SaldoInsuficienteException>();
        p.SaldoAtual.Should().Be(2);
        p.Movimentacoes.Should().HaveCount(1);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void RegistrarEntrada_ComQuantidadeNaoPositiva_DeveLancar(int q)
    {
        var p = Nova();
        var act = () => p.RegistrarEntrada(q, "x");
        act.Should().Throw<MovimentacaoInvalidaException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void RegistrarSaida_ComQuantidadeNaoPositiva_DeveLancar(int q)
    {
        var p = Nova();
        p.RegistrarEntrada(10, "x");
        var act = () => p.RegistrarSaida(q, "y", null);
        act.Should().Throw<MovimentacaoInvalidaException>();
    }

    [Fact]
    public void AtualizarDados_DeveAlterarNomeEPrecoMasNaoSku()
    {
        var p = Nova(50m);
        p.AtualizarDados("Filtro premium", 75m);
        p.Nome.Should().Be("Filtro premium");
        p.PrecoUnitario.Should().Be(75m);
        p.Sku.Valor.Should().Be("ABC-123");
    }

    [Fact]
    public void Inativar_NaoImpedeConsultaDeSaldo()
    {
        var p = Nova();
        p.RegistrarEntrada(5, "x");
        p.Inativar();

        p.Ativo.Should().BeFalse();
        p.SaldoAtual.Should().Be(5);
    }
}
