using Oficina.Aplicacao.Clientes.Gateways;
using Oficina.Dominio.Clientes;

namespace Oficina.Aplicacao.Clientes;

public class RemoverVeiculoUseCase
{
    private readonly IClienteGateway _gateway;
    public RemoverVeiculoUseCase(IClienteGateway gateway) => _gateway = gateway;

    public async Task<bool> ExecutarAsync(Guid clienteId, string placaBruta, CancellationToken ct)
    {
        var cliente = await _gateway.ObterPorIdAsync(clienteId, ct);
        if (cliente is null) return false;

        cliente.RemoverVeiculo(Placa.Criar(placaBruta));
        await _gateway.SalvarAsync(ct);
        return true;
    }
}
