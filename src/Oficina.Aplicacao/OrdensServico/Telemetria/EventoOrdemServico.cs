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

    public static EventoOrdemServico DeFalha(long numeroOs, string statusAtual, string unidade) =>
        new(numeroOs, statusAtual, statusAtual, null, ResultadoFalha, unidade);
}
