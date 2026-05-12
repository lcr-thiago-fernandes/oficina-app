using Oficina.Aplicacao.OrdensServico.Dtos;
using Oficina.Dominio.OrdensServico;

namespace Oficina.Aplicacao.OrdensServico;

public class ObterTempoMedioExecucaoUseCase
{
    private readonly IOrdemDeServicoRepositorio _repo;
    public ObterTempoMedioExecucaoUseCase(IOrdemDeServicoRepositorio repo) => _repo = repo;

    public async Task<MetricasTempoMedioResponse> ExecutarAsync(CancellationToken ct)
    {
        var m = await _repo.ObterTempoMedioExecucaoAsync(ct);
        return new MetricasTempoMedioResponse(
            m.TotalOrdensConcluidas,
            m.TempoMedio?.TotalMinutes,
            m.TempoMinimo?.TotalMinutes,
            m.TempoMaximo?.TotalMinutes);
    }
}
