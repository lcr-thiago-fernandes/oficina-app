using Oficina.Aplicacao.Catalogo.Dtos;
using Oficina.Aplicacao.Catalogo.Gateways;
using Oficina.Dominio.Catalogo;

namespace Oficina.Aplicacao.Catalogo;

public class CriarServicoUseCase
{
    private readonly IServicoGateway _gateway;
    public CriarServicoUseCase(IServicoGateway gateway) => _gateway = gateway;

    public async Task<Servico> ExecutarAsync(CriarServicoRequest req, CancellationToken ct)
    {
        var servico = Servico.Criar(req.Nome, req.Descricao, req.PrecoBase, req.TempoEstimadoMinutos);
        await _gateway.AdicionarAsync(servico, ct);
        await _gateway.SalvarAsync(ct);
        return servico;
    }
}
