using Oficina.Aplicacao.OrdensServico.Dtos;
using Oficina.Dominio.OrdensServico;

namespace Oficina.Aplicacao.OrdensServico;

public class EnviarOrcamentoParaAprovacaoUseCase
{
    private readonly IOrdemDeServicoRepositorio _repo;
    public EnviarOrcamentoParaAprovacaoUseCase(IOrdemDeServicoRepositorio repo) => _repo = repo;

    public async Task<OrdemResponse?> ExecutarAsync(Guid id, CancellationToken ct)
    {
        var os = await _repo.ObterPorIdAsync(id, ct);
        if (os is null) return null;
        os.EnviarOrcamentoParaAprovacao();
        await _repo.SalvarAsync(ct);
        return MapeadorOrdem.Mapear(os);
    }
}
