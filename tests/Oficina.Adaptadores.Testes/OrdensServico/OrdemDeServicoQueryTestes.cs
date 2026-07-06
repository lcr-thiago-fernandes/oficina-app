using FluentAssertions;
using Oficina.Adaptadores.OrdensServico.DataSources;
using Oficina.Dominio.OrdensServico;

namespace Oficina.Adaptadores.Testes.OrdensServico;

public class OrdemDeServicoQueryTestes
{
    // Fábricas que dirigem a OS até cada status usando apenas o domínio.
    private static OrdemDeServico Recebida()
    {
        var os = OrdemDeServico.Criar(Guid.NewGuid(), Guid.NewGuid());
        os.AdicionarItemServico(Guid.NewGuid(), "S", 10m, 1);
        return os;
    }

    private static OrdemDeServico EmDiagnostico()
    {
        var os = Recebida();
        os.IniciarDiagnostico();
        return os;
    }

    private static OrdemDeServico AguardandoAprovacao()
    {
        var os = EmDiagnostico();
        os.EnviarOrcamentoParaAprovacao();
        return os;
    }

    private static OrdemDeServico EmExecucao()
    {
        var os = AguardandoAprovacao();
        os.Aprovar();
        os.IniciarExecucao();
        return os;
    }

    private static OrdemDeServico Finalizada()
    {
        var os = EmExecucao();
        os.Finalizar();
        return os;
    }

    private static OrdemDeServico Entregue()
    {
        var os = Finalizada();
        os.Entregar();
        return os;
    }

    private static OrdemDeServico Cancelada()
    {
        var os = AguardandoAprovacao();
        os.Rejeitar();
        return os;
    }

    [Fact]
    public void AplicarFiltro_SemFiltro_ExcluiTerminais()
    {
        var fonte = new[]
        {
            Recebida(), EmDiagnostico(), AguardandoAprovacao(), EmExecucao(),
            Finalizada(), Entregue(), Cancelada()
        }.AsQueryable();

        var resultado = OrdemDeServicoQuery.AplicarFiltro(fonte, null).ToList();

        resultado.Should().OnlyContain(o =>
            o.Status != StatusOrdemDeServico.Finalizada &&
            o.Status != StatusOrdemDeServico.Entregue &&
            o.Status != StatusOrdemDeServico.Cancelada);
        resultado.Should().HaveCount(4);
    }

    [Fact]
    public void AplicarFiltro_ComStatusTerminal_IncluiApenasEle()
    {
        var fonte = new[] { Recebida(), Entregue(), Cancelada() }.AsQueryable();

        var resultado = OrdemDeServicoQuery
            .AplicarFiltro(fonte, StatusOrdemDeServico.Entregue).ToList();

        resultado.Should().ContainSingle()
            .Which.Status.Should().Be(StatusOrdemDeServico.Entregue);
    }

    [Fact]
    public void AplicarOrdenacao_OrdenaPorPrioridadeDeStatus()
    {
        // fonte propositalmente fora de ordem
        var fonte = new[]
        {
            Recebida(), EmExecucao(), EmDiagnostico(), AguardandoAprovacao()
        }.AsQueryable();

        var ordenado = OrdemDeServicoQuery.AplicarOrdenacao(fonte)
            .Select(o => o.Status).ToList();

        ordenado.Should().Equal(
            StatusOrdemDeServico.EmExecucao,
            StatusOrdemDeServico.AguardandoAprovacao,
            StatusOrdemDeServico.EmDiagnostico,
            StatusOrdemDeServico.Recebida);
    }

    [Fact]
    public void FiltroMaisOrdenacao_SemFiltro_ExcluiTerminaisEOrdena()
    {
        var fonte = new[]
        {
            Entregue(), Recebida(), Cancelada(), EmExecucao(),
            Finalizada(), EmDiagnostico(), AguardandoAprovacao()
        }.AsQueryable();

        var filtrado = OrdemDeServicoQuery.AplicarFiltro(fonte, null);
        var resultado = OrdemDeServicoQuery.AplicarOrdenacao(filtrado)
            .Select(o => o.Status).ToList();

        resultado.Should().Equal(
            StatusOrdemDeServico.EmExecucao,
            StatusOrdemDeServico.AguardandoAprovacao,
            StatusOrdemDeServico.EmDiagnostico,
            StatusOrdemDeServico.Recebida);
    }
}
