using FluentAssertions;
using Moq;
using Oficina.Aplicacao.OrdensServico;
using Oficina.Aplicacao.OrdensServico.Telemetria;
using Oficina.Dominio.Estoque;
using Oficina.Dominio.OrdensServico;
using Xunit;

namespace Oficina.Aplicacao.Testes.OrdensServico;

public class PublicacaoEventoOsTestes
{
    [Fact]
    public void DeUltimaTransicao_projeta_a_entrada_mais_recente_do_historico()
    {
        var os = OrdemDeServico.Criar(Guid.NewGuid(), Guid.NewGuid(), null, "filial-sp");
        os.IniciarDiagnostico();

        var evento = EventoOrdemServico.DeUltimaTransicao(os);

        evento.StatusAnterior.Should().Be("Recebida");
        evento.StatusNovo.Should().Be("EmDiagnostico");
        evento.Unidade.Should().Be("filial-sp");
        evento.Resultado.Should().Be("Sucesso");
        evento.DuracaoNoStatusSegundos.Should().NotBeNull();
    }

    [Fact]
    public void DeFalha_marca_o_resultado_como_Falha()
    {
        var evento = EventoOrdemServico.DeFalha(42, "EmExecucao", "matriz");

        evento.NumeroOs.Should().Be(42);
        evento.StatusAnterior.Should().Be("EmExecucao");
        evento.Resultado.Should().Be("Falha");
    }

    [Fact]
    public void Publicador_recebe_o_evento_da_transicao()
    {
        var publicador = new Mock<IPublicadorEventoOs>();
        var os = OrdemDeServico.Criar(Guid.NewGuid(), Guid.NewGuid());
        os.IniciarDiagnostico();

        publicador.Object.Publicar(EventoOrdemServico.DeUltimaTransicao(os));

        publicador.Verify(p => p.Publicar(
            It.Is<EventoOrdemServico>(e => e.StatusNovo == "EmDiagnostico")), Times.Once);
    }

    [Fact]
    public void DeFalha_nao_usa_um_valor_do_vocabulario_de_status_em_StatusNovo()
    {
        var evento = EventoOrdemServico.DeFalha(42, "Recebida", "matriz");

        // O painel de volume diário é `count(*) ... WHERE statusNovo = 'Recebida'`,
        // sem filtro de resultado: se a falha reaproveitasse o status atual, toda
        // tentativa FALHA de abertura seria contada como OS criada.
        evento.StatusNovo.Should().Be(EventoOrdemServico.StatusNovoFalha);
        Enum.GetNames<StatusOrdemDeServico>().Should().NotContain(evento.StatusNovo);
        evento.StatusAnterior.Should().Be("Recebida");
        evento.DuracaoNoStatusSegundos.Should().BeNull();
    }

    [Theory]
    [MemberData(nameof(ExcecoesDeNegocio))]
    public async Task Helper_relanca_erro_de_negocio_sem_publicar_evento_de_falha(Exception excecao)
    {
        var publicador = new Mock<IPublicadorEventoOs>();
        var os = OrdemDeServico.Criar(Guid.NewGuid(), Guid.NewGuid());

        var act = async () => await publicador.Object.ExecutarTransicaoComTelemetriaAsync(
            os, () => throw excecao);

        (await act.Should().ThrowAsync<Exception>()).Which.Should().BeSameAs(excecao);
        publicador.Verify(p => p.Publicar(It.IsAny<EventoOrdemServico>()), Times.Never);
    }

    public static TheoryData<Exception> ExcecoesDeNegocio() => new()
    {
        new TransicaoDeStatusInvalidaException(StatusOrdemDeServico.Recebida, "iniciar execução"),
        new OrdemSemItensException(),
        new OrcamentoNaoAprovadoException(),
        new OrdemInvalidaException("cliente inativo"),
        new SaldoInsuficienteException("ABC-123", 1, 5),
        new OperationCanceledException()
    };

    [Fact]
    public async Task Helper_publica_falha_para_erro_de_infraestrutura()
    {
        var publicador = new Mock<IPublicadorEventoOs>();
        var os = OrdemDeServico.Criar(Guid.NewGuid(), Guid.NewGuid());

        var act = async () => await publicador.Object.ExecutarTransicaoComTelemetriaAsync(
            os, () => throw new TimeoutException("banco indisponível"));

        await act.Should().ThrowAsync<TimeoutException>();
        publicador.Verify(p => p.Publicar(
            It.Is<EventoOrdemServico>(e => e.Resultado == EventoOrdemServico.ResultadoFalha)), Times.Once);
    }

    [Theory]
    [MemberData(nameof(ExcecoesDeNegocio))]
    public void EhFalhaDeProcessamento_e_falso_para_erro_de_negocio(Exception excecao) =>
        PublicadorEventoOsExtensions.EhFalhaDeProcessamento(excecao).Should().BeFalse();

    [Fact]
    public void EhFalhaDeProcessamento_e_verdadeiro_para_erro_inesperado() =>
        PublicadorEventoOsExtensions.EhFalhaDeProcessamento(new InvalidOperationException())
            .Should().BeTrue();
}
