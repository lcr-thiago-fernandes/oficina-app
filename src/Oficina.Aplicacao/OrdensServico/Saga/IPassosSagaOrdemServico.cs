using Oficina.Aplicacao.OrdensServico.Saga.Dtos;

namespace Oficina.Aplicacao.OrdensServico.Saga;

/// <summary>
/// Efeitos locais (OS e estoque) de cada passo da saga. Todos são idempotentes: a mensagem
/// pode ser reentregue (SQS at-least-once), e um passo já aplicado retorna sem persistir nada.
/// OS inexistente → <see cref="InvalidOperationException"/> (falha de processamento).
/// </summary>
public interface IPassosSagaOrdemServico
{
    Task<PedidoOrcamento> MontarPedidoOrcamentoAsync(Guid ordemServicoId, CancellationToken ct);
    Task RegistrarOrcamentoGeradoAsync(Guid ordemServicoId, CancellationToken ct);
    Task RegistrarOrcamentoAprovadoAsync(Guid ordemServicoId, CancellationToken ct);
    Task RegistrarOrcamentoRejeitadoAsync(Guid ordemServicoId, string? motivo, CancellationToken ct);

    /// <summary>
    /// Baixa o estoque reservado de cada peça (a OS continua <c>AguardandoPagamento</c>) e devolve o
    /// pedido para a Execução. Repetido → não baixa de novo e devolve o mesmo pedido.
    /// Devolve <c>null</c>, sem lançar e sem mexer em nada, quando o pagamento não pode mais ser
    /// honrado (OS cancelada, ou reserva de alguma peça já liberada/reposta): a saga deve então
    /// enviar <c>EstornarPagamento</c>.
    /// </summary>
    Task<PedidoExecucao?> ConfirmarPagamentoAsync(Guid ordemServicoId, CancellationToken ct);

    Task RegistrarExecucaoEnfileiradaAsync(Guid ordemServicoId, CancellationToken ct);

    /// <summary>
    /// Avança a OS para a etapa informada pela Execução. Devolve <c>false</c> (no-op) quando a OS já está
    /// nessa etapa ou além dela, ou cancelada: status repetido/atrasado não é um passo novo da trilha.
    /// </summary>
    Task<bool> RegistrarStatusExecucaoAsync(Guid ordemServicoId, EtapaExecucao etapa, CancellationToken ct);

    Task RegistrarExecucaoFinalizadaAsync(Guid ordemServicoId, CancellationToken ct);

    /// <summary>
    /// Desfaz o estoque da OS por peça (Reservada → libera; Confirmada → repõe; demais → nada) e
    /// cancela a OS se ainda não estiver <c>Cancelada</c>. Repetido → não mexe no estoque de novo.
    /// </summary>
    Task CancelarAsync(Guid ordemServicoId, string motivo, CancellationToken ct);
}
