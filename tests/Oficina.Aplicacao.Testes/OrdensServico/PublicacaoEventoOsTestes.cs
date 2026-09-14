using FluentAssertions;
using Moq;
using Oficina.Aplicacao.OrdensServico.Telemetria;
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
}
