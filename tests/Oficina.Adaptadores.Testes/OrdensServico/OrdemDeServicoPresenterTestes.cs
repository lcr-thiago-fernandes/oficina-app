using System.Linq;
using FluentAssertions;
using Oficina.Adaptadores.OrdensServico.Presenters;
using Oficina.Aplicacao.OrdensServico.Gateways;
using Oficina.Dominio.OrdensServico;

namespace Oficina.Adaptadores.Testes.OrdensServico;

public class OrdemDeServicoPresenterTestes
{
    private static OrdemDeServico CriarOrdemComItens()
    {
        var os = OrdemDeServico.Criar(Guid.NewGuid(), Guid.NewGuid(), "obs");
        os.AdicionarItemServico(Guid.NewGuid(), "Troca de óleo", 150m, 1);
        os.AdicionarItemPeca(Guid.NewGuid(), "Filtro", 25m, 2);
        return os;
    }

    [Fact]
    public void Apresentar_DeveMapearCamposEItens()
    {
        var os = CriarOrdemComItens();

        var resp = OrdemDeServicoPresenter.Apresentar(os);

        resp.Id.Should().Be(os.Id);
        resp.Status.Should().Be("Recebida");
        resp.Observacoes.Should().Be("obs");
        resp.TotalServicos.Should().Be(150m);
        resp.TotalPecas.Should().Be(50m);
        resp.TotalGeral.Should().Be(200m);
        resp.ItensServico.Should().ContainSingle(i => i.Nome == "Troca de óleo");
        resp.ItensPeca.Should().ContainSingle(i => i.Nome == "Filtro");
    }

    [Fact]
    public void ApresentarItemServico_DeveMapearCampos()
    {
        var os = CriarOrdemComItens();
        var item = os.ItensServico.First();

        var resp = OrdemDeServicoPresenter.ApresentarItemServico(item);

        resp.Id.Should().Be(item.Id);
        resp.ServicoId.Should().Be(item.ServicoId);
        resp.Nome.Should().Be("Troca de óleo");
        resp.PrecoUnitario.Should().Be(150m);
        resp.Quantidade.Should().Be(1);
        resp.Subtotal.Should().Be(150m);
    }

    [Fact]
    public void ApresentarItemPeca_DeveMapearCampos()
    {
        var os = CriarOrdemComItens();
        var item = os.ItensPeca.First();

        var resp = OrdemDeServicoPresenter.ApresentarItemPeca(item);

        resp.Id.Should().Be(item.Id);
        resp.PecaId.Should().Be(item.PecaId);
        resp.Nome.Should().Be("Filtro");
        resp.PrecoUnitario.Should().Be(25m);
        resp.Quantidade.Should().Be(2);
        resp.Subtotal.Should().Be(50m);
    }

    [Fact]
    public void ApresentarPagina_DeveMapearItensEMetadados()
    {
        var itens = new[] { CriarOrdemComItens() };

        var pagina = OrdemDeServicoPresenter.ApresentarPagina(itens, total: 1, pagina: 1, tamanhoPagina: 20);

        pagina.Total.Should().Be(1);
        pagina.Pagina.Should().Be(1);
        pagina.TamanhoPagina.Should().Be(20);
        pagina.Itens.Should().ContainSingle(o => o.Status == "Recebida");
    }

    [Fact]
    public void ApresentarMetrica_DeveConverterTimeSpanParaMinutos()
    {
        var metrica = new MetricaTempoMedio(
            TotalOrdensConcluidas: 3,
            TempoMedio: TimeSpan.FromMinutes(90),
            TempoMinimo: TimeSpan.FromMinutes(30),
            TempoMaximo: TimeSpan.FromMinutes(150));

        var resp = OrdemDeServicoPresenter.ApresentarMetrica(metrica);

        resp.TotalOrdensConcluidas.Should().Be(3);
        resp.TempoMedioMinutos.Should().Be(90);
        resp.TempoMinimoMinutos.Should().Be(30);
        resp.TempoMaximoMinutos.Should().Be(150);
    }

    [Fact]
    public void ApresentarMetrica_SemOrdens_DeveManterNulos()
    {
        var metrica = new MetricaTempoMedio(0, null, null, null);

        var resp = OrdemDeServicoPresenter.ApresentarMetrica(metrica);

        resp.TotalOrdensConcluidas.Should().Be(0);
        resp.TempoMedioMinutos.Should().BeNull();
        resp.TempoMinimoMinutos.Should().BeNull();
        resp.TempoMaximoMinutos.Should().BeNull();
    }
}
