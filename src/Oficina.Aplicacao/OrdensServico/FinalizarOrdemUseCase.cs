using Oficina.Aplicacao.OrdensServico.Gateways;
using Oficina.Dominio.OrdensServico;

namespace Oficina.Aplicacao.OrdensServico;

public class FinalizarOrdemUseCase
{
    private readonly IOrdemDeServicoGateway _gateway;
    public FinalizarOrdemUseCase(IOrdemDeServicoGateway gateway) => _gateway = gateway;

    public async Task<OrdemDeServico?> ExecutarAsync(Guid id, CancellationToken ct)
    {
        var os = await _gateway.ObterPorIdAsync(id, ct);
        if (os is null) return null;
        os.Finalizar();
        await _gateway.SalvarAsync(ct);
        return os;
    }
}
