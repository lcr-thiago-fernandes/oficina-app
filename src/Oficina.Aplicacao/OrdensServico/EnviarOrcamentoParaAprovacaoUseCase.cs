using Oficina.Aplicacao.OrdensServico.Gateways;
using Oficina.Dominio.OrdensServico;

namespace Oficina.Aplicacao.OrdensServico;

public class EnviarOrcamentoParaAprovacaoUseCase
{
    private readonly IOrdemDeServicoGateway _gateway;
    public EnviarOrcamentoParaAprovacaoUseCase(IOrdemDeServicoGateway gateway) => _gateway = gateway;

    public async Task<OrdemDeServico?> ExecutarAsync(Guid id, CancellationToken ct)
    {
        var os = await _gateway.ObterPorIdAsync(id, ct);
        if (os is null) return null;
        os.EnviarOrcamentoParaAprovacao();
        await _gateway.SalvarAsync(ct);
        return os;
    }
}
