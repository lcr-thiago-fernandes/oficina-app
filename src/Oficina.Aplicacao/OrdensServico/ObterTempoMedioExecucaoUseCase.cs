using Oficina.Aplicacao.OrdensServico.Gateways;

namespace Oficina.Aplicacao.OrdensServico;

public class ObterTempoMedioExecucaoUseCase
{
    private readonly IOrdemDeServicoGateway _gateway;
    public ObterTempoMedioExecucaoUseCase(IOrdemDeServicoGateway gateway) => _gateway = gateway;

    public Task<MetricaTempoMedio> ExecutarAsync(CancellationToken ct) =>
        _gateway.ObterTempoMedioExecucaoAsync(ct);
}
