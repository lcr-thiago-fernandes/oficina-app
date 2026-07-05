using FluentAssertions;
using Oficina.Adaptadores.Catalogo.Presenters;
using Oficina.Dominio.Catalogo;

namespace Oficina.Adaptadores.Testes.Catalogo;

public class ServicoPresenterTestes
{
    [Fact]
    public void Apresentar_DeveMapearTodosOsCamposDaEntidade()
    {
        var servico = Servico.Criar("Troca de óleo", "desc", 150m, 30);

        var resp = ServicoPresenter.Apresentar(servico);

        resp.Id.Should().Be(servico.Id);
        resp.Nome.Should().Be("Troca de óleo");
        resp.PrecoBase.Should().Be(150m);
        resp.TempoEstimadoMinutos.Should().Be(30);
        resp.Ativo.Should().BeTrue();
    }

    [Fact]
    public void ApresentarPagina_DeveMapearItensEMetadados()
    {
        var itens = new[] { Servico.Criar("A", "x", 10m, 5), Servico.Criar("B", "y", 20m, 10) };

        var pagina = ServicoPresenter.ApresentarPagina(itens, total: 2, pagina: 1, tamanhoPagina: 20);

        pagina.Total.Should().Be(2);
        pagina.Pagina.Should().Be(1);
        pagina.TamanhoPagina.Should().Be(20);
        pagina.Itens.Should().HaveCount(2);
        pagina.Itens.Select(i => i.Nome).Should().ContainInOrder("A", "B");
    }
}
