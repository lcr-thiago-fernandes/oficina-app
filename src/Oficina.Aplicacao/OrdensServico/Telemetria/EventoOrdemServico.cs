using Oficina.Dominio.OrdensServico;

namespace Oficina.Aplicacao.OrdensServico.Telemetria;

/// <summary>
/// Evento de negócio enviado ao New Relic. Alimenta os dois painéis obrigatórios
/// da Fase 3 — volume diário de OS e tempo médio por status — e o alerta de
/// falha no processamento.
/// </summary>
public sealed record EventoOrdemServico(
    long NumeroOs,
    string? StatusAnterior,
    string StatusNovo,
    long? DuracaoNoStatusSegundos,
    string Resultado,
    string Unidade)
{
    public const string ResultadoSucesso = "Sucesso";
    public const string ResultadoFalha = "Falha";

    /// <summary>
    /// Valor de <c>statusNovo</c> usado nos eventos de falha. É deliberadamente
    /// um valor FORA do vocabulário de <see cref="StatusOrdemDeServico"/>:
    /// as consultas dos painéis da Fase 3 filtram por <c>statusNovo</c> sem
    /// filtrar <c>resultado</c> (o painel de volume diário é
    /// <c>count(*) ... WHERE statusNovo = 'Recebida'</c>), então qualquer status
    /// real aqui faria uma tentativa FALHA de abertura ser contada como OS
    /// criada — e o mesmo vale para o painel de tempo por status. Com um valor
    /// fora do vocabulário, nenhuma consulta por status pode casar com um evento
    /// de falha, independentemente do caso de uso que falhou.
    /// </summary>
    public const string StatusNovoFalha = "NaoAplicavel";

    public static EventoOrdemServico DeUltimaTransicao(OrdemDeServico os)
    {
        var ultima = os.Historico.Last();
        return new EventoOrdemServico(
            os.Numero,
            ultima.StatusAnterior?.ToString(),
            ultima.StatusNovo.ToString(),
            ultima.DuracaoSegundos,
            ResultadoSucesso,
            os.Unidade);
    }

    /// <summary>
    /// Evento de falha de processamento. <paramref name="statusAtual"/> vai em
    /// <c>statusAnterior</c> (contexto de diagnóstico: onde a OS estava quando
    /// falhou) e <c>statusNovo</c> recebe <see cref="StatusNovoFalha"/>, porque
    /// não houve transição alguma.
    /// </summary>
    public static EventoOrdemServico DeFalha(long numeroOs, string statusAtual, string unidade) =>
        new(numeroOs, statusAtual, StatusNovoFalha, null, ResultadoFalha, unidade);
}
