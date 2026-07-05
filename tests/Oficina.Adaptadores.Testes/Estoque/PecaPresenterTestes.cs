using System.Linq;
using FluentAssertions;
using Oficina.Adaptadores.Estoque.Presenters;
using Oficina.Dominio.Estoque;

namespace Oficina.Adaptadores.Testes.Estoque;

public class PecaPresenterTestes
{
    private static Peca CriarPecaComMovimentacao()
    {
        var peca = Peca.Criar(Sku.Criar("ABC-123"), "Filtro", 25m);
        peca.RegistrarEntrada(10, "compra inicial");
        return peca;
    }

    [Fact]
    public void Apresentar_DeveMapearTodosOsCamposDaEntidade()
    {
        var peca = CriarPecaComMovimentacao();

        var resp = PecaPresenter.Apresentar(peca);

        resp.Id.Should().Be(peca.Id);
        resp.Sku.Should().Be("ABC-123");
        resp.Nome.Should().Be("Filtro");
        resp.PrecoUnitario.Should().Be(25m);
        resp.SaldoAtual.Should().Be(10);
        resp.Ativo.Should().BeTrue();
    }

    [Fact]
    public void ApresentarMovimentacao_DeveMapearCampos()
    {
        var peca = CriarPecaComMovimentacao();
        var mov = peca.Movimentacoes.First();

        var resp = PecaPresenter.ApresentarMovimentacao(mov);

        resp.Id.Should().Be(mov.Id);
        resp.Tipo.Should().Be("Entrada");
        resp.Quantidade.Should().Be(10);
        resp.Motivo.Should().Be("compra inicial");
        resp.OrdemServicoId.Should().BeNull();
    }

    [Fact]
    public void ApresentarPagina_DeveMapearItensEMetadados()
    {
        var itens = new[] { CriarPecaComMovimentacao() };

        var pagina = PecaPresenter.ApresentarPagina(itens, total: 1, pagina: 1, tamanhoPagina: 20);

        pagina.Total.Should().Be(1);
        pagina.Pagina.Should().Be(1);
        pagina.TamanhoPagina.Should().Be(20);
        pagina.Itens.Should().ContainSingle(p => p.Sku == "ABC-123");
    }
}
