using Oficina.Aplicacao.OrdensServico.Gateways;
using Oficina.Aplicacao.OrdensServico.Telemetria;
using Oficina.Dominio.OrdensServico;

namespace Oficina.Aplicacao.OrdensServico;

public class EnviarOrcamentoParaAprovacaoUseCase
{
    private readonly IOrdemDeServicoGateway _gateway;
    private readonly INotificacaoGateway _notificacoes;
    private readonly IPublicadorEventoOs _publicador;

    public EnviarOrcamentoParaAprovacaoUseCase(
        IOrdemDeServicoGateway gateway, INotificacaoGateway notificacoes, IPublicadorEventoOs publicador)
    {
        _gateway = gateway;
        _notificacoes = notificacoes;
        _publicador = publicador;
    }

    public async Task<OrdemDeServico?> ExecutarAsync(Guid id, CancellationToken ct)
    {
        var os = await _gateway.ObterPorIdAsync(id, ct);
        if (os is null) return null;

        await _publicador.ExecutarTransicaoComTelemetriaAsync(os, async () =>
        {
            os.EnviarOrcamentoParaAprovacao();
            await _gateway.SalvarAsync(ct);
        });

        await _notificacoes.NotificarMudancaDeStatusAsync(os, ct);
        return os;
    }
}
