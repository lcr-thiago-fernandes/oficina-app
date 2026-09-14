using Oficina.Aplicacao.OrdensServico.Gateways;
using Oficina.Aplicacao.OrdensServico.Telemetria;
using Oficina.Dominio.OrdensServico;

namespace Oficina.Aplicacao.OrdensServico;

// Registra a decisão do cliente sobre o orçamento (via webhook externo).
// aprovado=true → Aprovar(); aprovado=false → Rejeitar() (cancela a OS).
// Transições inválidas lançam TransicaoDeStatusInvalidaException (→ 422 no middleware).
public class RegistrarDecisaoDeOrcamentoUseCase
{
    private readonly IOrdemDeServicoGateway _ordens;
    private readonly INotificacaoGateway _notificacoes;
    private readonly IPublicadorEventoOs _publicador;

    public RegistrarDecisaoDeOrcamentoUseCase(
        IOrdemDeServicoGateway ordens, INotificacaoGateway notificacoes, IPublicadorEventoOs publicador)
    {
        _ordens = ordens;
        _notificacoes = notificacoes;
        _publicador = publicador;
    }

    public async Task<OrdemDeServico?> ExecutarAsync(Guid ordemId, bool aprovado, CancellationToken ct)
    {
        var ordem = await _ordens.ObterPorIdAsync(ordemId, ct);
        if (ordem is null) return null;

        // Aprovar() só carimba OrcamentoAprovadoEm e não muda o Status nem
        // acrescenta entrada ao Historico — não há transição para publicar.
        // Rejeitar() cancela a OS e essa transição alimenta os painéis.
        await _publicador.ExecutarTransicaoComTelemetriaAsync(
            ordem,
            async () =>
            {
                if (aprovado) ordem.Aprovar();
                else ordem.Rejeitar();

                await _ordens.SalvarAsync(ct);
            },
            publicarSucesso: !aprovado);

        await _notificacoes.NotificarMudancaDeStatusAsync(ordem, ct);
        return ordem;
    }
}
