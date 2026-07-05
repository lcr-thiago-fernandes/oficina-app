using Oficina.Aplicacao.Clientes.Gateways;
using Oficina.Dominio.Clientes;

namespace Oficina.Aplicacao.Clientes;

public class ObterClientePorIdUseCase
{
    private readonly IClienteGateway _gateway;
    public ObterClientePorIdUseCase(IClienteGateway gateway) => _gateway = gateway;

    public Task<Cliente?> ExecutarAsync(Guid id, CancellationToken ct) => _gateway.ObterPorIdAsync(id, ct);
}
