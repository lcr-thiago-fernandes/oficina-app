using FluentAssertions;
using Oficina.Adaptadores.OrdensServico.DataSources;
using Oficina.Dominio.OrdensServico;
using Oficina.Testes.Compartilhado;

namespace Oficina.Adaptadores.Testes.OrdensServico;

public class OrdemDeServicoQueryTestes
{
    private static OrdemDeServico Em(StatusOrdemDeServico status) => OrdemEmStatus.Criar(status);

    private static readonly StatusOrdemDeServico[] TodosOsStatus = Enum.GetValues<StatusOrdemDeServico>();

    [Fact]
    public void AplicarFiltro_SemFiltro_ExcluiTerminais()
    {
        var fonte = TodosOsStatus.Select(Em).AsQueryable();

        var resultado = OrdemDeServicoQuery.AplicarFiltro(fonte, null).ToList();

        resultado.Should().OnlyContain(o =>
            o.Status != StatusOrdemDeServico.Finalizada &&
            o.Status != StatusOrdemDeServico.Entregue &&
            o.Status != StatusOrdemDeServico.Cancelada);
        resultado.Should().HaveCount(6);
    }

    [Fact]
    public void AplicarFiltro_ComStatusTerminal_IncluiApenasEle()
    {
        var fonte = new[]
        {
            Em(StatusOrdemDeServico.Recebida), Em(StatusOrdemDeServico.Entregue), Em(StatusOrdemDeServico.Cancelada)
        }.AsQueryable();

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
            Em(StatusOrdemDeServico.Recebida),
            Em(StatusOrdemDeServico.Finalizada),
            Em(StatusOrdemDeServico.AguardandoAprovacao),
            Em(StatusOrdemDeServico.EmFilaExecucao),
            Em(StatusOrdemDeServico.EmReparo),
            Em(StatusOrdemDeServico.AguardandoPagamento),
            Em(StatusOrdemDeServico.EmDiagnostico),
        }.AsQueryable();

        var ordenado = OrdemDeServicoQuery.AplicarOrdenacao(fonte)
            .Select(o => o.Status).ToList();

        ordenado.Should().Equal(
            StatusOrdemDeServico.EmReparo,
            StatusOrdemDeServico.EmDiagnostico,
            StatusOrdemDeServico.EmFilaExecucao,
            StatusOrdemDeServico.AguardandoPagamento,
            StatusOrdemDeServico.AguardandoAprovacao,
            StatusOrdemDeServico.Recebida,
            StatusOrdemDeServico.Finalizada);
    }

    [Fact]
    public void FiltroMaisOrdenacao_SemFiltro_ExcluiTerminaisEOrdena()
    {
        var fonte = new[]
        {
            Em(StatusOrdemDeServico.Entregue), Em(StatusOrdemDeServico.Recebida),
            Em(StatusOrdemDeServico.Cancelada), Em(StatusOrdemDeServico.EmReparo),
            Em(StatusOrdemDeServico.Finalizada), Em(StatusOrdemDeServico.EmDiagnostico),
            Em(StatusOrdemDeServico.AguardandoAprovacao), Em(StatusOrdemDeServico.AguardandoPagamento),
            Em(StatusOrdemDeServico.EmFilaExecucao)
        }.AsQueryable();

        var filtrado = OrdemDeServicoQuery.AplicarFiltro(fonte, null);
        var resultado = OrdemDeServicoQuery.AplicarOrdenacao(filtrado)
            .Select(o => o.Status).ToList();

        resultado.Should().Equal(
            StatusOrdemDeServico.EmReparo,
            StatusOrdemDeServico.EmDiagnostico,
            StatusOrdemDeServico.EmFilaExecucao,
            StatusOrdemDeServico.AguardandoPagamento,
            StatusOrdemDeServico.AguardandoAprovacao,
            StatusOrdemDeServico.Recebida);
    }
}
