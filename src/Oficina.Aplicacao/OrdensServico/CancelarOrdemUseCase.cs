using Oficina.Aplicacao.OrdensServico.Dtos;
using Oficina.Aplicacao.OrdensServico.Gateways;
using Oficina.Aplicacao.OrdensServico.Saga;
using Oficina.Dominio.OrdensServico;

namespace Oficina.Aplicacao.OrdensServico;

/// <summary>
/// Pedido de cancelamento do atendente.
/// <list type="bullet">
/// <item>OS com saga (<see cref="OrdemDeServico.CoordenadaPorSaga"/>): não muda o status; enfileira
/// <c>CancelamentoSolicitado</c> no outbox e quem cancela (e desfaz o estoque) é a saga →
/// <see cref="FormaCancelamento.Solicitado"/>.</item>
/// <item>OS da Fase 3 (sem saga, nunca terá): cancela na hora pelos mesmos passos da saga
/// (inclusive o tratamento da saída manual legada) → <see cref="FormaCancelamento.Concluido"/>.</item>
/// </list>
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
    private readonly IPassosSagaOrdemServico _passos;

    public CancelarOrdemUseCase(
        IOrdemDeServicoGateway ordens, ISagaOrdemServicoGateway saga, IPassosSagaOrdemServico passos)
    {
        _ordens = ordens;
        _saga = saga;
        _passos = passos;
    }

    public async Task<ResultadoCancelamentoOrdem?> ExecutarAsync(Guid id, string motivo, CancellationToken ct)
    {
        var os = await _ordens.ObterPorIdAsync(id, ct);
        if (os is null) return null;

        if (!os.PodeSerCanceladaPeloAtendente)
            throw new TransicaoDeStatusInvalidaException(os.Status, "cancelar");

        if (!os.CoordenadaPorSaga)
        {
            // CancelarAsync recarrega a OS pelo mesmo DbContext scoped: é a mesma instância rastreada.
            await _passos.CancelarAsync(id, motivo.Trim(), ct);
            return new ResultadoCancelamentoOrdem(os, FormaCancelamento.Concluido);
        }

        // A OS fica AguardandoPagamento até a ExecucaoEnfileirada, mesmo com o pagamento já
        // confirmado: nessa janela a saga ignoraria o pedido e a API devolveria 202 sem efeito.
        // Saga ainda inexistente (outbox não entregou OrdemServicoAberta) → permitido.
        // Resta uma janela estreita entre esta leitura e o consumo do pedido; nela a saga registra
        // o cancelamento como ignorado na trilha.
        var saga = await _saga.ObterEstadoAsync(id, ct);
        if (saga is not null && !EstadosSagaCancelaveis.Contains(saga.Estado))
            throw new TransicaoDeStatusInvalidaException(os.Status, "cancelar");

        await _saga.SolicitarCancelamentoAsync(id, motivo.Trim(), ct);
        await _ordens.SalvarAsync(ct);
        return new ResultadoCancelamentoOrdem(os, FormaCancelamento.Solicitado);
    }
}
