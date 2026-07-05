using FluentAssertions;
using Oficina.Dominio.OrdensServico;
using Xunit;

namespace Oficina.Dominio.Testes.OrdensServico;

public class OrdemDeServicoTestes
{
    private static OrdemDeServico Nova() =>
        OrdemDeServico.Criar(clienteId: Guid.NewGuid(), veiculoId: Guid.NewGuid());

    private static OrdemDeServico ComItens()
    {
        var os = Nova();
        os.AdicionarItemServico(Guid.NewGuid(), "Troca de óleo", 150m, 1);
        os.AdicionarItemPeca(Guid.NewGuid(), "Filtro", 25m, 1);
        return os;
    }

    [Fact]
    public void Criar_DeveIniciarComStatusRecebida()
    {
        var os = Nova();

        os.Id.Should().NotBeEmpty();
        os.Status.Should().Be(StatusOrdemDeServico.Recebida);
        os.ItensServico.Should().BeEmpty();
        os.ItensPeca.Should().BeEmpty();
        os.TotalGeral.Should().Be(0m);
        os.OrcamentoAprovadoEm.Should().BeNull();
    }

    [Fact]
    public void IniciarDiagnostico_FromRecebida_DeveAvancar()
    {
        var os = Nova();
        os.IniciarDiagnostico();

        os.Status.Should().Be(StatusOrdemDeServico.EmDiagnostico);
        os.DiagnosticadaEm.Should().NotBeNull();
    }

    [Fact]
    public void IniciarDiagnostico_FromEmDiagnostico_DeveLancar()
    {
        var os = Nova();
        os.IniciarDiagnostico();
        var act = () => os.IniciarDiagnostico();
        act.Should().Throw<TransicaoDeStatusInvalidaException>();
    }

    [Fact]
    public void AdicionarItemServico_AntesDeExecucao_DeveIncluir()
    {
        var os = Nova();
        os.AdicionarItemServico(Guid.NewGuid(), "Alinhamento", 90m, 1);

        os.ItensServico.Should().HaveCount(1);
        os.TotalServicos.Should().Be(90m);
    }

    [Fact]
    public void AdicionarItemPeca_AntesDeExecucao_DeveIncluir()
    {
        var os = Nova();
        os.AdicionarItemPeca(Guid.NewGuid(), "Pastilha", 80m, 2);

        os.ItensPeca.Should().HaveCount(1);
        os.TotalPecas.Should().Be(160m);
        os.TotalGeral.Should().Be(160m);
    }

    [Fact]
    public void TotalGeral_DeveSomarServicosEPecas()
    {
        var os = Nova();
        os.AdicionarItemServico(Guid.NewGuid(), "S", 100m, 1);
        os.AdicionarItemPeca(Guid.NewGuid(), "P", 50m, 2);

        os.TotalServicos.Should().Be(100m);
        os.TotalPecas.Should().Be(100m);
        os.TotalGeral.Should().Be(200m);
    }

    [Fact]
    public void EnviarOrcamentoParaAprovacao_SemItens_DeveLancar()
    {
        var os = Nova();
        os.IniciarDiagnostico();

        var act = () => os.EnviarOrcamentoParaAprovacao();

        act.Should().Throw<OrdemSemItensException>();
    }

    [Fact]
    public void EnviarOrcamentoParaAprovacao_FromRecebida_DeveLancar()
    {
        var os = Nova();
        os.AdicionarItemServico(Guid.NewGuid(), "x", 10m, 1);

        var act = () => os.EnviarOrcamentoParaAprovacao();

        act.Should().Throw<TransicaoDeStatusInvalidaException>();
    }

    [Fact]
    public void EnviarOrcamentoParaAprovacao_FromEmDiagnosticoComItens_DeveAvancar()
    {
        var os = ComItens();
        os.IniciarDiagnostico();

        os.EnviarOrcamentoParaAprovacao();

        os.Status.Should().Be(StatusOrdemDeServico.AguardandoAprovacao);
        os.EnviadaAprovacaoEm.Should().NotBeNull();
    }

    [Fact]
    public void Aprovar_FromAguardandoAprovacao_DeveMarcarAprovacaoSemMudarStatus()
    {
        var os = ComItens();
        os.IniciarDiagnostico();
        os.EnviarOrcamentoParaAprovacao();

        os.Aprovar();

        os.Status.Should().Be(StatusOrdemDeServico.AguardandoAprovacao);
        os.OrcamentoAprovadoEm.Should().NotBeNull();
    }

    [Fact]
    public void Aprovar_QuandoNaoEstaAguardando_DeveLancar()
    {
        var os = Nova();
        var act = () => os.Aprovar();
        act.Should().Throw<TransicaoDeStatusInvalidaException>();
    }

    [Fact]
    public void Rejeitar_FromAguardandoAprovacao_DeveCancelar()
    {
        var os = ComItens();
        os.IniciarDiagnostico();
        os.EnviarOrcamentoParaAprovacao();

        os.Rejeitar();

        os.Status.Should().Be(StatusOrdemDeServico.Cancelada);
        os.OrcamentoRejeitadoEm.Should().NotBeNull();
    }

    [Fact]
    public void IniciarExecucao_SemAprovacao_DeveLancar()
    {
        var os = ComItens();
        os.IniciarDiagnostico();
        os.EnviarOrcamentoParaAprovacao();

        var act = () => os.IniciarExecucao();

        act.Should().Throw<OrcamentoNaoAprovadoException>();
    }

    [Fact]
    public void IniciarExecucao_AposAprovacao_DeveAvancar()
    {
        var os = ComItens();
        os.IniciarDiagnostico();
        os.EnviarOrcamentoParaAprovacao();
        os.Aprovar();

        os.IniciarExecucao();

        os.Status.Should().Be(StatusOrdemDeServico.EmExecucao);
        os.IniciadaEm.Should().NotBeNull();
    }

    [Fact]
    public void Finalizar_FromEmExecucao_DeveAvancar()
    {
        var os = ComItens();
        os.IniciarDiagnostico();
        os.EnviarOrcamentoParaAprovacao();
        os.Aprovar();
        os.IniciarExecucao();

        os.Finalizar();

        os.Status.Should().Be(StatusOrdemDeServico.Finalizada);
        os.FinalizadaEm.Should().NotBeNull();
    }

    [Fact]
    public void Entregar_FromFinalizada_DeveAvancar()
    {
        var os = ComItens();
        os.IniciarDiagnostico();
        os.EnviarOrcamentoParaAprovacao();
        os.Aprovar();
        os.IniciarExecucao();
        os.Finalizar();

        os.Entregar();

        os.Status.Should().Be(StatusOrdemDeServico.Entregue);
        os.EntregueEm.Should().NotBeNull();
    }

    [Fact]
    public void AdicionarItemServico_DepoisDeIniciarExecucao_DeveLancar()
    {
        var os = ComItens();
        os.IniciarDiagnostico();
        os.EnviarOrcamentoParaAprovacao();
        os.Aprovar();
        os.IniciarExecucao();

        var act = () => os.AdicionarItemServico(Guid.NewGuid(), "x", 10m, 1);

        act.Should().Throw<OrdemImutavelException>();
    }

    [Fact]
    public void RemoverItemServico_AntesDeExecucao_DeveRemover()
    {
        var os = Nova();
        os.AdicionarItemServico(Guid.NewGuid(), "S", 10m, 1);
        var item = os.ItensServico.Single();

        os.RemoverItemServico(item.Id);

        os.ItensServico.Should().BeEmpty();
    }

    [Fact]
    public void RemoverItemServico_ItemInexistente_DeveLancar()
    {
        var os = Nova();
        var act = () => os.RemoverItemServico(Guid.NewGuid());
        act.Should().Throw<ItemNaoEncontradoException>();
    }

    [Fact]
    public void RemoverItemPeca_AntesDeExecucao_DeveRemover()
    {
        var os = Nova();
        os.AdicionarItemPeca(Guid.NewGuid(), "P", 10m, 1);
        var item = os.ItensPeca.Single();

        os.RemoverItemPeca(item.Id);

        os.ItensPeca.Should().BeEmpty();
    }

    [Fact]
    public void TempoExecucao_AposEntrega_DeveSerDuracaoEntreInicioEFim()
    {
        var os = ComItens();
        os.IniciarDiagnostico();
        os.EnviarOrcamentoParaAprovacao();
        os.Aprovar();
        os.IniciarExecucao();

        // simular alguma duração — o teste valida a propriedade existir
        os.Finalizar();
        os.Entregar();

        var dur = os.DuracaoExecucao;
        dur.Should().NotBeNull();
        dur!.Value.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
    }
}
