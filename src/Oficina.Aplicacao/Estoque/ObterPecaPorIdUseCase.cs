using Oficina.Aplicacao.Estoque.Gateways;
using Oficina.Dominio.Estoque;

namespace Oficina.Aplicacao.Estoque;

public class ObterPecaPorIdUseCase
{
    private readonly IPecaGateway _gateway;
    public ObterPecaPorIdUseCase(IPecaGateway gateway) => _gateway = gateway;

    public Task<Peca?> ExecutarAsync(Guid id, CancellationToken ct) => _gateway.ObterPorIdAsync(id, ct);
}
