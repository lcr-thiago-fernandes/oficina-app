using FluentAssertions;
using Oficina.Dominio.OrdensServico;
using Xunit;

namespace Oficina.Dominio.Testes.OrdensServico;

public class OrdemDeServicoTestes
{
    private static OrdemDeServico Nova() =>
        OrdemDeServico.Criar(clienteId: Guid.NewGuid(), veiculoId: Guid.NewGuid());

    /// <summary>
    /// Cria a OS com 1 serviço e aplica a cadeia feliz até <paramref name="alvo"/>.
    /// <see cref="StatusOrdemDeServico.Cancelada"/> é alcançada cancelando a partir de Recebida.
    /// </summary>
    private static OrdemDeServico OsEm(StatusOrdemDeServico alvo)
    {
        var os = Nova();
        os.AdicionarItemServico(Guid.NewGuid(), "Troca de óleo", 150m, 1);

        if (alvo == StatusOrdemDeServico.Cancelada)
        {
            os.Cancelar("motivo");
            return os;
        }

        var cadeia = new Action<OrdemDeServico>[]
        {
            o => o.AguardarAprovacao(),
            o => o.AguardarPagamento(),
            o => o.EnfileirarExecucao(),
            o => o.IniciarDiagnostico(),
            o => o.IniciarReparo(),
            o => o.Finalizar(),
            o => o.Entregar(),
        };

        foreach (var passo in cadeia)
        {
            if (os.Status == alvo) break;
            passo(os);
        }

        os.Status.Should().Be(alvo);
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
        os.MotivoCancelamento.Should().BeNull();
        os.CanceladaEm.Should().BeNull();
        os.PodeSerCanceladaPeloAtendente.Should().BeTrue();
    }

    [Fact]
    public void AdicionarItemServico_EmRecebida_DeveIncluir()
    {
        var os = Nova();
        os.AdicionarItemServico(Guid.NewGuid(), "Alinhamento", 90m, 1);

        os.ItensServico.Should().HaveCount(1);
        os.TotalServicos.Should().Be(90m);
    }

    [Fact]
    public void AdicionarItemPeca_EmRecebida_DeveIncluir()
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
    public void AguardarAprovacao_DeRecebidaComItens_Avanca()
    {
        var os = OsEm(StatusOrdemDeServico.Recebida);

        os.AguardarAprovacao();

        os.Status.Should().Be(StatusOrdemDeServico.AguardandoAprovacao);
        os.EnviadaAprovacaoEm.Should().NotBeNull();
    }

    [Fact]
    public void AguardarAprovacao_SemItens_LancaOrdemSemItens()
    {
        var os = Nova();

        var act = () => os.AguardarAprovacao();

        act.Should().Throw<OrdemSemItensException>();
    }

    [Fact]
    public void AguardarAprovacao_ForaDeRecebida_LancaTransicaoInvalida()
    {
        var os = OsEm(StatusOrdemDeServico.AguardandoAprovacao);

        var act = () => os.AguardarAprovacao();

        act.Should().Throw<TransicaoDeStatusInvalidaException>();
    }

    [Fact]
    public void AguardarPagamento_DeAguardandoAprovacao_Avanca()
    {
        var os = OsEm(StatusOrdemDeServico.AguardandoAprovacao);

        os.AguardarPagamento();

        os.Status.Should().Be(StatusOrdemDeServico.AguardandoPagamento);
        os.OrcamentoAprovadoEm.Should().NotBeNull();
    }

    [Fact]
    public void AguardarPagamento_DeRecebida_LancaTransicaoInvalida()
    {
        var os = OsEm(StatusOrdemDeServico.Recebida);

        var act = () => os.AguardarPagamento();

        act.Should().Throw<TransicaoDeStatusInvalidaException>();
    }

    [Fact]
    public void Rejeitar_DeAguardandoAprovacao_CancelaComMotivoPadrao()
    {
        var os = OsEm(StatusOrdemDeServico.AguardandoAprovacao);

        os.Rejeitar();

        os.Status.Should().Be(StatusOrdemDeServico.Cancelada);
        os.OrcamentoRejeitadoEm.Should().NotBeNull();
        os.CanceladaEm.Should().NotBeNull();
        os.MotivoCancelamento.Should().Be("Orçamento rejeitado pelo cliente");
    }

    [Fact]
    public void Rejeitar_ComMotivo_UsaMotivoInformado()
    {
        var os = OsEm(StatusOrdemDeServico.AguardandoAprovacao);

        os.Rejeitar("caro demais");

        os.MotivoCancelamento.Should().Be("caro demais");
    }

    [Fact]
    public void Rejeitar_ForaDeAguardandoAprovacao_LancaTransicaoInvalida()
    {
        var os = OsEm(StatusOrdemDeServico.AguardandoPagamento);

        var act = () => os.Rejeitar();

        act.Should().Throw<TransicaoDeStatusInvalidaException>();
    }

    [Fact]
    public void EnfileirarExecucao_DeAguardandoPagamento_Avanca()
    {
        var os = OsEm(StatusOrdemDeServico.AguardandoPagamento);

        os.EnfileirarExecucao();

        os.Status.Should().Be(StatusOrdemDeServico.EmFilaExecucao);
    }

    [Fact]
    public void EnfileirarExecucao_DeAguardandoAprovacao_LancaTransicaoInvalida()
    {
        var os = OsEm(StatusOrdemDeServico.AguardandoAprovacao);

        var act = () => os.EnfileirarExecucao();

        act.Should().Throw<TransicaoDeStatusInvalidaException>();
    }

    [Fact]
    public void IniciarDiagnostico_DeEmFilaExecucao_Avanca()
    {
        var os = OsEm(StatusOrdemDeServico.EmFilaExecucao);

        os.IniciarDiagnostico();

        os.Status.Should().Be(StatusOrdemDeServico.EmDiagnostico);
        os.DiagnosticadaEm.Should().NotBeNull();
    }

    [Fact]
    public void IniciarDiagnostico_DeRecebida_LancaTransicaoInvalida()
    {
        var os = OsEm(StatusOrdemDeServico.Recebida);

        var act = () => os.IniciarDiagnostico();

        act.Should().Throw<TransicaoDeStatusInvalidaException>();
    }

    [Fact]
    public void IniciarReparo_DeEmFilaExecucao_PulaDiagnostico()
    {
        var os = OsEm(StatusOrdemDeServico.EmFilaExecucao);

        os.IniciarReparo();

        os.Status.Should().Be(StatusOrdemDeServico.EmReparo);
        os.IniciadaEm.Should().NotBeNull();
        os.DiagnosticadaEm.Should().BeNull();
    }

    [Fact]
    public void IniciarReparo_DeEmDiagnostico_Avanca()
    {
        var os = OsEm(StatusOrdemDeServico.EmDiagnostico);

        os.IniciarReparo();

        os.Status.Should().Be(StatusOrdemDeServico.EmReparo);
        os.IniciadaEm.Should().NotBeNull();
    }

    [Fact]
    public void IniciarReparo_DeAguardandoPagamento_LancaTransicaoInvalida()
    {
        var os = OsEm(StatusOrdemDeServico.AguardandoPagamento);

        var act = () => os.IniciarReparo();

        act.Should().Throw<TransicaoDeStatusInvalidaException>();
    }

    [Fact]
    public void Finalizar_DeEmFilaExecucao_Avanca()
    {
        var os = OsEm(StatusOrdemDeServico.EmFilaExecucao);

        os.Finalizar();

        os.Status.Should().Be(StatusOrdemDeServico.Finalizada);
        os.FinalizadaEm.Should().NotBeNull();
    }

    [Theory]
    [InlineData(StatusOrdemDeServico.EmDiagnostico)]
    [InlineData(StatusOrdemDeServico.EmReparo)]
    public void Finalizar_DeExecucaoEmAndamento_Avanca(StatusOrdemDeServico origem)
    {
        var os = OsEm(origem);

        os.Finalizar();

        os.Status.Should().Be(StatusOrdemDeServico.Finalizada);
        os.FinalizadaEm.Should().NotBeNull();
    }

    [Fact]
    public void Finalizar_DeAguardandoPagamento_LancaTransicaoInvalida()
    {
        var os = OsEm(StatusOrdemDeServico.AguardandoPagamento);

        var act = () => os.Finalizar();

        act.Should().Throw<TransicaoDeStatusInvalidaException>();
    }

    [Fact]
    public void Entregar_DeFinalizada_Avanca()
    {
        var os = OsEm(StatusOrdemDeServico.Finalizada);

        os.Entregar();

        os.Status.Should().Be(StatusOrdemDeServico.Entregue);
        os.EntregueEm.Should().NotBeNull();
    }

    [Fact]
    public void Entregar_DeEmReparo_LancaTransicaoInvalida()
    {
        var os = OsEm(StatusOrdemDeServico.EmReparo);

        var act = () => os.Entregar();

        act.Should().Throw<TransicaoDeStatusInvalidaException>();
    }

    [Theory]
    [InlineData(StatusOrdemDeServico.Recebida)]
    [InlineData(StatusOrdemDeServico.AguardandoAprovacao)]
    [InlineData(StatusOrdemDeServico.AguardandoPagamento)]
    public void Cancelar_EmStatusPermitido_Cancela(StatusOrdemDeServico origem)
    {
        var os = OsEm(origem);
        os.PodeSerCanceladaPeloAtendente.Should().BeTrue();

        os.Cancelar("motivo");

        os.Status.Should().Be(StatusOrdemDeServico.Cancelada);
        os.CanceladaEm.Should().NotBeNull();
        os.MotivoCancelamento.Should().Be("motivo");
        os.Historico.Last().StatusAnterior.Should().Be(origem);
        os.Historico.Last().StatusNovo.Should().Be(StatusOrdemDeServico.Cancelada);
    }

    [Theory]
    [InlineData(StatusOrdemDeServico.EmFilaExecucao)]
    [InlineData(StatusOrdemDeServico.Finalizada)]
    [InlineData(StatusOrdemDeServico.Cancelada)]
    public void Cancelar_ForaDoPermitido_Lanca(StatusOrdemDeServico origem)
    {
        var os = OsEm(origem);
        os.PodeSerCanceladaPeloAtendente.Should().BeFalse();

        var act = () => os.Cancelar("motivo");

        act.Should().Throw<TransicaoDeStatusInvalidaException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Cancelar_SemMotivo_LancaArgumentException(string? motivo)
    {
        var os = OsEm(StatusOrdemDeServico.Recebida);

        var act = () => os.Cancelar(motivo!);

        act.Should().Throw<ArgumentException>();
        os.Status.Should().Be(StatusOrdemDeServico.Recebida);
    }

    [Fact]
    public void AdicionarItemServico_ForaDeRecebida_LancaOrdemImutavel()
    {
        var os = OsEm(StatusOrdemDeServico.AguardandoAprovacao);

        var act = () => os.AdicionarItemServico(Guid.NewGuid(), "x", 10m, 1);

        act.Should().Throw<OrdemImutavelException>();
    }

    [Fact]
    public void AdicionarItemPeca_ForaDeRecebida_LancaOrdemImutavel()
    {
        var os = OsEm(StatusOrdemDeServico.AguardandoPagamento);

        var act = () => os.AdicionarItemPeca(Guid.NewGuid(), "x", 10m, 1);

        act.Should().Throw<OrdemImutavelException>();
    }

    [Fact]
    public void JaAlcancou_RespeitaOrdemDoFluxo()
    {
        StatusOrdemDeServico.EmReparo.JaAlcancou(StatusOrdemDeServico.EmDiagnostico).Should().BeTrue();
        StatusOrdemDeServico.EmDiagnostico.JaAlcancou(StatusOrdemDeServico.EmReparo).Should().BeFalse();
        StatusOrdemDeServico.Cancelada.JaAlcancou(StatusOrdemDeServico.Recebida).Should().BeFalse();
        StatusOrdemDeServico.Recebida.JaAlcancou(StatusOrdemDeServico.Cancelada).Should().BeFalse();
        StatusOrdemDeServico.EmFilaExecucao.JaAlcancou(StatusOrdemDeServico.EmFilaExecucao).Should().BeTrue();
    }

    [Fact]
    public void Etapa_SegueAOrdemDoFluxo()
    {
        new[]
        {
            StatusOrdemDeServico.Recebida,
            StatusOrdemDeServico.AguardandoAprovacao,
            StatusOrdemDeServico.AguardandoPagamento,
            StatusOrdemDeServico.EmFilaExecucao,
            StatusOrdemDeServico.EmDiagnostico,
            StatusOrdemDeServico.EmReparo,
            StatusOrdemDeServico.Finalizada,
            StatusOrdemDeServico.Entregue,
        }.Select(s => s.Etapa()).Should().Equal(1, 2, 3, 4, 5, 6, 7, 8);
        StatusOrdemDeServico.Cancelada.Etapa().Should().Be(int.MaxValue);
    }

    [Fact]
    public void HistoricoCompleto_DoCaminhoFeliz_TemOitoEntradas()
    {
        var os = OsEm(StatusOrdemDeServico.Entregue);

        os.Historico.Should().HaveCount(8);
        os.Historico.Select(h => h.StatusNovo).Should().Equal(
            StatusOrdemDeServico.Recebida,
            StatusOrdemDeServico.AguardandoAprovacao,
            StatusOrdemDeServico.AguardandoPagamento,
            StatusOrdemDeServico.EmFilaExecucao,
            StatusOrdemDeServico.EmDiagnostico,
            StatusOrdemDeServico.EmReparo,
            StatusOrdemDeServico.Finalizada,
            StatusOrdemDeServico.Entregue);
    }

    [Fact]
    public void DuracaoExecucao_AposEntrega_DeveSerDuracaoEntreInicioEFim()
    {
        var os = OsEm(StatusOrdemDeServico.Entregue);

        var dur = os.DuracaoExecucao;
        dur.Should().NotBeNull();
        dur!.Value.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
    }

    [Fact]
    public void Criar_NaoNasceCoordenadaPorSaga()
    {
        // OSs da Fase 3 (e qualquer OS criada fora da abertura da Fase 4) não têm saga.
        Nova().CoordenadaPorSaga.Should().BeFalse();
    }

    [Fact]
    public void MarcarCoordenadaPorSaga_Marca_EIdempotente()
    {
        var os = Nova();

        os.MarcarCoordenadaPorSaga();
        os.MarcarCoordenadaPorSaga();

        os.CoordenadaPorSaga.Should().BeTrue();
    }
}
