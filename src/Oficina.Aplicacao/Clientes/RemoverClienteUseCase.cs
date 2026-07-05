using Oficina.Aplicacao.Clientes.Gateways;

namespace Oficina.Aplicacao.Clientes;

public class RemoverClienteUseCase
{
    private readonly IClienteGateway _gateway;
    public RemoverClienteUseCase(IClienteGateway gateway) => _gateway = gateway;

    public async Task<bool> ExecutarAsync(Guid id, CancellationToken ct)
    {
        var cliente = await _gateway.ObterPorIdAsync(id, ct);
        if (cliente is null) return false;

        cliente.Inativar(); // soft delete
        await _gateway.SalvarAsync(ct);
        return true;
    }
}
