namespace Oficina.Aplicacao.OrdensServico.Saga.Telemetria;

/// <summary>
/// Passo da saga da OS enviado ao New Relic como custom event <c>SagaOrdemServico</c>. Alimenta o dashboard
/// "Saga da OS" (passos, compensações, duração) e o alerta de compensação que falhou.
/// </summary>
/// <param name="Passo">Nome do passo (ex.: <c>GerarOrcamento</c>, <c>EstornarPagamento</c>).</param>
/// <param name="Resultado"><c>Sucesso</c>, <c>Falha</c>, <c>Compensado</c>, <c>CompensacaoFalhou</c> ou <c>Ignorado</c>.</param>
/// <param name="DuracaoMs">Tempo desde o passo anterior da mesma saga.</param>
/// <param name="Motivo">Razão de negócio, presente em toda compensação/falha (ex.: cartao_recusado).</param>
/// <param name="Detalhe">Informação extra do passo (PagamentoId, status da execução...), sem ser razão.</param>
/// <param name="Estado">Estado da saga depois do passo.</param>
public sealed record EventoSagaOrdemServico(
    Guid OrdemServicoId,
    long NumeroOs,
    string Passo,
    string Resultado,
    bool Compensacao,
    long DuracaoMs,
    string Estado,
    string? Motivo,
    string? Detalhe = null);
