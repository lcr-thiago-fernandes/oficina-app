using Oficina.Aplicacao.OrdensServico.Gateways;
using Oficina.Aplicacao.OrdensServico.Saga;
using Oficina.Dominio.OrdensServico;

namespace Oficina.Aplicacao.OrdensServico;

/// <summary>
/// Pedido de cancelamento do atendente. Não muda o status da OS: enfileira
/// <c>CancelamentoSolicitado</c> no outbox e quem cancela (e desfaz o estoque) é a saga.
/// </summary>
public class CancelarOrdemUseCase
{
    // Estados da saga em que ela ainda aceita CancelamentoSolicitado (antes do pagamento confirmado).
    private static readonly HashSet<string> EstadosSagaCancelaveis = new(StringComparer.Ordinal)
    {
        "AguardandoOrcamento",
        "AguardandoAprovacao",
        "AguardandoPagamento"
    };

    private readonly IOrdemDeServicoGateway _ordens;
    private readonly ISagaOrdemServicoGateway _saga;

    public CancelarOrdemUseCase(IOrdemDeServicoGateway ordens, ISagaOrdemServicoGateway saga)
    {
        _ordens = ordens;
        _saga = saga;
    }

    public async Task<OrdemDeServico?> ExecutarAsync(Guid id, string motivo, CancellationToken ct)
    {
        var os = await _ordens.ObterPorIdAsync(id, ct);
        if (os is null) return null;

        if (!os.PodeSerCanceladaPeloAtendente)
            throw new TransicaoDeStatusInvalidaException(os.Status, "cancelar");

        // A OS fica AguardandoPagamento até a ExecucaoEnfileirada, mesmo com o pagamento já
        // confirmado: nessa janela a saga ignoraria o pedido e a API devolveria 202 sem efeito.
        // Saga ainda inexistente (outbox não entregou OrdemServicoAberta) → permitido.
        var saga = await _saga.ObterEstadoAsync(id, ct);
        if (saga is not null && !EstadosSagaCancelaveis.Contains(saga.Estado))
            throw new TransicaoDeStatusInvalidaException(os.Status, "cancelar");

        await _saga.SolicitarCancelamentoAsync(id, motivo.Trim(), ct);
        await _ordens.SalvarAsync(ct);
        return os;
    }
}
