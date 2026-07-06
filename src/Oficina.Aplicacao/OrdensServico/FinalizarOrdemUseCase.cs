using Oficina.Aplicacao.OrdensServico.Gateways;
using Oficina.Dominio.OrdensServico;

namespace Oficina.Aplicacao.OrdensServico;

public class FinalizarOrdemUseCase
{
    private readonly IOrdemDeServicoGateway _gateway;
    private readonly INotificacaoGateway _notificacoes;

    public FinalizarOrdemUseCase(IOrdemDeServicoGateway gateway, INotificacaoGateway notificacoes)
    {
        _gateway = gateway;
        _notificacoes = notificacoes;
    }

    public async Task<OrdemDeServico?> ExecutarAsync(Guid id, CancellationToken ct)
    {
        var os = await _gateway.ObterPorIdAsync(id, ct);
        if (os is null) return null;
        os.Finalizar();
        await _gateway.SalvarAsync(ct);
        await _notificacoes.NotificarMudancaDeStatusAsync(os, ct);
        return os;
    }
}
