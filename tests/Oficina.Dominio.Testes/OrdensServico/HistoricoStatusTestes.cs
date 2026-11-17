using FluentAssertions;
using Oficina.Dominio.OrdensServico;
using Xunit;

namespace Oficina.Dominio.Testes.OrdensServico;

public class HistoricoStatusTestes
{
    private static OrdemDeServico NovaOrdem() =>
        OrdemDeServico.Criar(Guid.NewGuid(), Guid.NewGuid());

    private static OrdemDeServico NovaOrdemComItem()
    {
        var os = NovaOrdem();
        os.AdicionarItemServico(Guid.NewGuid(), "Revisao", 50m, 1);
        return os;
    }

    [Fact]
    public void Criar_registra_a_primeira_transicao_para_Recebida()
    {
        var os = NovaOrdem();

        os.Historico.Should().HaveCount(1);
        var h = os.Historico.Single();
        h.StatusAnterior.Should().BeNull();
        h.StatusNovo.Should().Be(StatusOrdemDeServico.Recebida);
        h.DuracaoSegundos.Should().BeNull();
    }

    [Fact]
    public void AguardarAprovacao_registra_transicao_com_duracao_do_status_anterior()
    {
        var os = NovaOrdemComItem();

        os.AguardarAprovacao();

        os.Historico.Should().HaveCount(2);
        var h = os.Historico.Last();
        h.StatusAnterior.Should().Be(StatusOrdemDeServico.Recebida);
        h.StatusNovo.Should().Be(StatusOrdemDeServico.AguardandoAprovacao);
        h.DuracaoSegundos.Should().NotBeNull();
        h.DuracaoSegundos.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public void Ciclo_sem_diagnostico_registra_uma_entrada_por_transicao()
    {
        var os = NovaOrdemComItem();
        os.AguardarAprovacao();
        os.AguardarPagamento();
        os.EnfileirarExecucao();
        os.IniciarReparo();
        os.Finalizar();
        os.Entregar();

        os.Historico.Select(h => h.StatusNovo).Should().Equal(
            StatusOrdemDeServico.Recebida,
            StatusOrdemDeServico.AguardandoAprovacao,
            StatusOrdemDeServico.AguardandoPagamento,
            StatusOrdemDeServico.EmFilaExecucao,
            StatusOrdemDeServico.EmReparo,
            StatusOrdemDeServico.Finalizada,
            StatusOrdemDeServico.Entregue);
        os.Historico.Skip(1).Select(h => h.StatusAnterior).Should().Equal(
            os.Historico.Take(os.Historico.Count - 1).Select(h => (StatusOrdemDeServico?)h.StatusNovo));
    }

    [Fact]
    public void Rejeitar_registra_transicao_para_Cancelada()
    {
        var os = NovaOrdemComItem();
        os.AguardarAprovacao();

        os.Rejeitar();

        os.Historico.Last().StatusNovo.Should().Be(StatusOrdemDeServico.Cancelada);
        os.Historico.Last().StatusAnterior.Should().Be(StatusOrdemDeServico.AguardandoAprovacao);
    }

    [Fact]
    public void AguardarPagamento_registra_transicao()
    {
        var os = NovaOrdemComItem();
        os.AguardarAprovacao();

        os.AguardarPagamento();

        os.Historico.Last().StatusAnterior.Should().Be(StatusOrdemDeServico.AguardandoAprovacao);
        os.Historico.Last().StatusNovo.Should().Be(StatusOrdemDeServico.AguardandoPagamento);
    }

    [Fact]
    public void Criar_usa_matriz_como_unidade_padrao()
    {
        NovaOrdem().Unidade.Should().Be("matriz");
    }

    [Fact]
    public void Criar_aceita_unidade_informada()
    {
        var os = OrdemDeServico.Criar(Guid.NewGuid(), Guid.NewGuid(), null, "filial-sp");
        os.Unidade.Should().Be("filial-sp");
    }
}
