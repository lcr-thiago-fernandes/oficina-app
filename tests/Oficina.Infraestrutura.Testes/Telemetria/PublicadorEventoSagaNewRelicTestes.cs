using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Oficina.Aplicacao.OrdensServico.Saga.Telemetria;
using Oficina.Infraestrutura.Telemetria;
using Xunit;

namespace Oficina.Infraestrutura.Testes.Telemetria;

// Sem agente do New Relic no processo de testes: o publicador engole a falha da API estática.
public class PublicadorEventoSagaNewRelicTestes
{
    private static PublicadorEventoSagaNewRelic CriarPublicador() =>
        new(NullLogger<PublicadorEventoSagaNewRelic>.Instance);

    [Fact]
    public void PublicarEventoSagaNewRelic_SemAgente_NaoLanca()
    {
        var evento = new EventoSagaOrdemServico(Guid.NewGuid(), 10, "GerarOrcamento", "Sucesso", false, 120, "AguardandoAprovacao", null);

        var act = () => CriarPublicador().Publicar(evento);

        act.Should().NotThrow();
    }

    [Fact]
    public void PublicarEventoSagaNewRelic_ComMotivo_SemAgente_NaoLanca()
    {
        var evento = new EventoSagaOrdemServico(Guid.NewGuid(), 10, "EstornarPagamento", "CompensacaoFalhou", true, 5000, "Compensando", "gateway fora");

        var act = () => CriarPublicador().Publicar(evento);

        act.Should().NotThrow();
    }

    [Fact]
    public void MontarAtributos_OmiteMotivoNulo_EIncluiQuandoPreenchido()
    {
        var id = Guid.NewGuid();
        var sem = PublicadorEventoSagaNewRelic.MontarAtributos(
            new EventoSagaOrdemServico(id, 7, "Cancelamento", "Compensado", true, 42, "Cancelada", null));
        var com = PublicadorEventoSagaNewRelic.MontarAtributos(
            new EventoSagaOrdemServico(id, 7, "Cancelamento", "Compensado", true, 42, "Cancelada", "x"));

        sem.Should().NotContainKey("motivo");
        sem["ordemServicoId"].Should().Be(id.ToString());
        sem["numeroOs"].Should().Be(7L);
        sem["passo"].Should().Be("Cancelamento");
        sem["resultado"].Should().Be("Compensado");
        sem["compensacao"].Should().Be(true);
        sem["duracaoMs"].Should().Be(42L);
        sem["estado"].Should().Be("Cancelada");
        com["motivo"].Should().Be("x");
    }
}
