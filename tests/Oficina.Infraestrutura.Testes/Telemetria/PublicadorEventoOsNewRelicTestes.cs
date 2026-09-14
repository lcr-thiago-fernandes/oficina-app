using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Oficina.Aplicacao.OrdensServico.Telemetria;
using Oficina.Infraestrutura.Telemetria;
using Xunit;

namespace Oficina.Infraestrutura.Testes.Telemetria;

// O agente do New Relic não está anexado no processo de testes, então o
// publicador precisa engolir qualquer falha da chamada estática da API e
// nunca deixar a exceção subir — telemetria não pode derrubar requisição.
public class PublicadorEventoOsNewRelicTestes
{
    private static PublicadorEventoOsNewRelic CriarPublicador() =>
        new(NullLogger<PublicadorEventoOsNewRelic>.Instance);

    [Fact]
    public void Publicar_evento_de_sucesso_nao_lanca_excecao()
    {
        var evento = new EventoOrdemServico(1, "Recebida", "EmDiagnostico", 120, "Sucesso", "matriz");

        var act = () => CriarPublicador().Publicar(evento);

        act.Should().NotThrow();
    }

    [Fact]
    public void Publicar_evento_de_falha_sem_status_anterior_nao_lanca_excecao()
    {
        var evento = new EventoOrdemServico(2, null, "Recebida", null, "Falha", "filial-sp");

        var act = () => CriarPublicador().Publicar(evento);

        act.Should().NotThrow();
    }
}
