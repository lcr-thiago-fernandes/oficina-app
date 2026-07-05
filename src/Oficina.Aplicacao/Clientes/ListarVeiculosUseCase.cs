using Oficina.Aplicacao.Clientes.Gateways;
using Oficina.Dominio.Clientes;

namespace Oficina.Aplicacao.Clientes;

public class ListarVeiculosUseCase
{
    private readonly IClienteGateway _gateway;
    public ListarVeiculosUseCase(IClienteGateway gateway) => _gateway = gateway;

    public async Task<IReadOnlyList<Veiculo>?> ExecutarAsync(Guid clienteId, CancellationToken ct)
    {
        var cliente = await _gateway.ObterPorIdAsync(clienteId, ct);
        if (cliente is null) return null;
        return cliente.Veiculos.ToList();
    }
}
