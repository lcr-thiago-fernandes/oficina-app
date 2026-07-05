using Oficina.Aplicacao.Clientes.Dtos;
using Oficina.Aplicacao.Clientes.Gateways;
using Oficina.Dominio.Clientes;

namespace Oficina.Aplicacao.Clientes;

public class AtualizarClienteUseCase
{
    private readonly IClienteGateway _gateway;
    public AtualizarClienteUseCase(IClienteGateway gateway) => _gateway = gateway;

    public async Task<Cliente?> ExecutarAsync(Guid id, AtualizarClienteRequest req, CancellationToken ct)
    {
        var cliente = await _gateway.ObterPorIdAsync(id, ct);
        if (cliente is null) return null;

        cliente.AtualizarContato(
            req.Nome,
            Email.Criar(req.Email),
            Telefone.Criar(req.Telefone));

        await _gateway.SalvarAsync(ct);
        return cliente;
    }
}
