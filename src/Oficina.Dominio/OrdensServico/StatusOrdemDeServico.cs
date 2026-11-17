namespace Oficina.Dominio.OrdensServico;

public enum StatusOrdemDeServico
{
    Recebida = 1,
    EmDiagnostico = 2,
    AguardandoAprovacao = 3,
    EmReparo = 4,
    Finalizada = 5,
    Entregue = 6,
    Cancelada = 7,
    AguardandoPagamento = 8,
    EmFilaExecucao = 9
}

public static class StatusOrdemDeServicoExtensions
{
    /// <summary>
    /// Posição do status no fluxo da OS (os valores do enum não seguem a ordem do fluxo).
    /// <see cref="StatusOrdemDeServico.Cancelada"/> é terminal e fica fora da sequência.
    /// </summary>
    public static int Etapa(this StatusOrdemDeServico s) => s switch
    {
        StatusOrdemDeServico.Recebida => 1,
        StatusOrdemDeServico.AguardandoAprovacao => 2,
        StatusOrdemDeServico.AguardandoPagamento => 3,
        StatusOrdemDeServico.EmFilaExecucao => 4,
        StatusOrdemDeServico.EmDiagnostico => 5,
        StatusOrdemDeServico.EmReparo => 6,
        StatusOrdemDeServico.Finalizada => 7,
        StatusOrdemDeServico.Entregue => 8,
        StatusOrdemDeServico.Cancelada => int.MaxValue,
        _ => throw new ArgumentOutOfRangeException(nameof(s), s, "Status desconhecido.")
    };

    /// <summary>
    /// Indica se a OS em <paramref name="atual"/> já chegou (ou passou) de <paramref name="alvo"/>.
    /// Sempre <c>false</c> quando qualquer um dos dois é <see cref="StatusOrdemDeServico.Cancelada"/>.
    /// </summary>
    public static bool JaAlcancou(this StatusOrdemDeServico atual, StatusOrdemDeServico alvo) =>
        atual != StatusOrdemDeServico.Cancelada
        && alvo != StatusOrdemDeServico.Cancelada
        && atual.Etapa() >= alvo.Etapa();
}
