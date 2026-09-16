using Oficina.Dominio;
using Oficina.Dominio.OrdensServico;

namespace Oficina.Aplicacao.OrdensServico.Telemetria;

/// <summary>
/// Agrupa o padrão repetido em cada caso de uso que muda o estado da OS: executa
/// a transição/persistência informada e, se ela for bem-sucedida, publica o
/// evento projetado da última entrada do histórico; se lançar uma <b>falha de
/// processamento</b>, publica o evento de falha e relança a exceção original —
/// telemetria nunca engole erro, só o acompanha.
///
/// Erro de negócio (<see cref="ExcecaoDeDominio"/>, mapeado para 4xx) e
/// cancelamento do cliente (<see cref="OperationCanceledException"/>) sobem sem
/// evento de falha: são resultado esperado de uma requisição, não indisponibilidade.
///
/// Extraído na Task 10 (revisão, ACHADO 3) para não repetir o mesmo bloco
/// try/catch em cada caso de uso de transição — inclusive nos dois casos de uso
/// de criação de OS, que passaram a publicar o evento (ACHADO 1).
/// </summary>
public static class PublicadorEventoOsExtensions
{
    /// <summary>
    /// Classifica uma exceção como falha de processamento (infraestrutura, bug,
    /// timeout) — a única categoria que alimenta <c>resultado='Falha'</c> no
    /// evento <c>OrdemServicoEvento</c> e, por consequência, o alerta Critical.
    /// </summary>
    public static bool EhFalhaDeProcessamento(Exception ex) =>
        ex is not ExcecaoDeDominio and not OperationCanceledException;

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
        catch (Exception ex) when (EhFalhaDeProcessamento(ex))
        {
            publicador.Publicar(EventoOrdemServico.DeFalha(os.Numero, os.Status.ToString(), os.Unidade));
            throw;
        }
    }
}
