using Oficina.Dominio.OrdensServico;

namespace Oficina.Aplicacao.OrdensServico.Telemetria;

/// <summary>
/// Agrupa o padrão repetido em cada caso de uso que muda o estado da OS: executa
/// a transição/persistência informada e, se ela for bem-sucedida, publica o
/// evento projetado da última entrada do histórico; se lançar, publica o evento
/// de falha e relança a exceção original — telemetria nunca engole erro de
/// negócio, só o acompanha.
///
/// Extraído na Task 10 (revisão, ACHADO 3) para não repetir o mesmo bloco
/// try/catch em cada caso de uso de transição — inclusive nos dois casos de uso
/// de criação de OS, que passaram a publicar o evento (ACHADO 1).
/// </summary>
public static class PublicadorEventoOsExtensions
{
    public static async Task ExecutarTransicaoComTelemetriaAsync(
        this IPublicadorEventoOs publicador,
        OrdemDeServico os,
        Func<Task> transicaoEPersistencia,
        bool publicarSucesso = true)
    {
        try
        {
            await transicaoEPersistencia();

            if (publicarSucesso)
                publicador.Publicar(EventoOrdemServico.DeUltimaTransicao(os));
        }
        catch (Exception)
        {
            publicador.Publicar(EventoOrdemServico.DeFalha(os.Numero, os.Status.ToString(), os.Unidade));
            throw;
        }
    }
}
