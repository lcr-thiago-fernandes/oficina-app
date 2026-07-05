using Oficina.Aplicacao.Estoque.Gateways;

namespace Oficina.Aplicacao.Estoque;

public class RemoverPecaUseCase
{
    private readonly IPecaGateway _gateway;
    public RemoverPecaUseCase(IPecaGateway gateway) => _gateway = gateway;

    public async Task<bool> ExecutarAsync(Guid id, CancellationToken ct)
    {
        var p = await _gateway.ObterPorIdAsync(id, ct);
        if (p is null) return false;

        p.Inativar(); // soft delete
        await _gateway.SalvarAsync(ct);
        return true;
    }
}
