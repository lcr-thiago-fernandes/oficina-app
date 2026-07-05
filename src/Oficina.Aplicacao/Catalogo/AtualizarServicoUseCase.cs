using Oficina.Aplicacao.Catalogo.Dtos;
using Oficina.Aplicacao.Catalogo.Gateways;
using Oficina.Dominio.Catalogo;

namespace Oficina.Aplicacao.Catalogo;

public class AtualizarServicoUseCase
{
    private readonly IServicoGateway _gateway;
    public AtualizarServicoUseCase(IServicoGateway gateway) => _gateway = gateway;

    public async Task<Servico?> ExecutarAsync(Guid id, AtualizarServicoRequest req, CancellationToken ct)
    {
        var servico = await _gateway.ObterPorIdAsync(id, ct);
        if (servico is null) return null;

        servico.AtualizarDados(req.Nome, req.Descricao, req.PrecoBase, req.TempoEstimadoMinutos);
        await _gateway.SalvarAsync(ct);
        return servico;
    }
}
