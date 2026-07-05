using Oficina.Aplicacao.Catalogo.Gateways;
using Oficina.Dominio.Catalogo;

namespace Oficina.Aplicacao.Catalogo;

public class ObterServicoPorIdUseCase
{
    private readonly IServicoGateway _gateway;
    public ObterServicoPorIdUseCase(IServicoGateway gateway) => _gateway = gateway;

    public Task<Servico?> ExecutarAsync(Guid id, CancellationToken ct) => _gateway.ObterPorIdAsync(id, ct);
}
