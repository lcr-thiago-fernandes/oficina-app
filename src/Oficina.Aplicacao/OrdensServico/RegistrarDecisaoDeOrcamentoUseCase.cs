using Oficina.Aplicacao.OrdensServico.Gateways;
using Oficina.Dominio.OrdensServico;

namespace Oficina.Aplicacao.OrdensServico;

// Registra a decisão do cliente sobre o orçamento (via webhook externo).
// aprovado=true → Aprovar(); aprovado=false → Rejeitar() (cancela a OS).
// Transições inválidas lançam TransicaoDeStatusInvalidaException (→ 422 no middleware).
public class RegistrarDecisaoDeOrcamentoUseCase
{
    private readonly IOrdemDeServicoGateway _ordens;
    private readonly INotificacaoGateway _notificacoes;

    public RegistrarDecisaoDeOrcamentoUseCase(
        IOrdemDeServicoGateway ordens, INotificacaoGateway notificacoes)
    {
        _ordens = ordens;
        _notificacoes = notificacoes;
    }

    public async Task<OrdemDeServico?> ExecutarAsync(Guid ordemId, bool aprovado, CancellationToken ct)
    {
        var ordem = await _ordens.ObterPorIdAsync(ordemId, ct);
        if (ordem is null) return null;

        if (aprovado) ordem.Aprovar();
        else ordem.Rejeitar();

        await _ordens.SalvarAsync(ct);
        await _notificacoes.NotificarMudancaDeStatusAsync(ordem, ct);
        return ordem;
    }
}
