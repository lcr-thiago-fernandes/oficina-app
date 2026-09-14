using FluentAssertions;
using Oficina.Dominio.OrdensServico;
using Xunit;

namespace Oficina.Dominio.Testes.OrdensServico;

public class HistoricoStatusTestes
{
    private static OrdemDeServico NovaOrdem() =>
        OrdemDeServico.Criar(Guid.NewGuid(), Guid.NewGuid());

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
    public void IniciarDiagnostico_registra_transicao_com_duracao_do_status_anterior()
    {
        var os = NovaOrdem();

        os.IniciarDiagnostico();

        os.Historico.Should().HaveCount(2);
        var h = os.Historico.Last();
        h.StatusAnterior.Should().Be(StatusOrdemDeServico.Recebida);
        h.StatusNovo.Should().Be(StatusOrdemDeServico.EmDiagnostico);
        h.DuracaoSegundos.Should().NotBeNull();
        h.DuracaoSegundos.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public void Ciclo_completo_registra_uma_entrada_por_transicao()
    {
        var os = NovaOrdem();
        os.IniciarDiagnostico();
        os.AdicionarItemServico(Guid.NewGuid(), "Troca de oleo", 100m, 1);
        os.EnviarOrcamentoParaAprovacao();
        os.Aprovar();
        os.IniciarExecucao();
        os.Finalizar();
        os.Entregar();

        // Recebida, EmDiagnostico, AguardandoAprovacao, EmExecucao, Finalizada, Entregue
        os.Historico.Should().HaveCount(6);
        os.Historico.Select(h => h.StatusNovo).Should().ContainInOrder(
            StatusOrdemDeServico.Recebida,
            StatusOrdemDeServico.EmDiagnostico,
            StatusOrdemDeServico.AguardandoAprovacao,
            StatusOrdemDeServico.EmExecucao,
            StatusOrdemDeServico.Finalizada,
            StatusOrdemDeServico.Entregue);
    }

    [Fact]
    public void Rejeitar_registra_transicao_para_Cancelada()
    {
        var os = NovaOrdem();
        os.IniciarDiagnostico();
        os.AdicionarItemServico(Guid.NewGuid(), "Revisao", 50m, 1);
        os.EnviarOrcamentoParaAprovacao();

        os.Rejeitar();

        os.Historico.Last().StatusNovo.Should().Be(StatusOrdemDeServico.Cancelada);
        os.Historico.Last().StatusAnterior.Should().Be(StatusOrdemDeServico.AguardandoAprovacao);
    }

    [Fact]
    public void Aprovar_nao_registra_transicao_porque_nao_muda_o_status()
    {
        var os = NovaOrdem();
        os.IniciarDiagnostico();
        os.AdicionarItemServico(Guid.NewGuid(), "Revisao", 50m, 1);
        os.EnviarOrcamentoParaAprovacao();
        var antes = os.Historico.Count;

        os.Aprovar();

        os.Historico.Should().HaveCount(antes);
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
