using Oficina.Aplicacao.Clientes.Dtos;
using Oficina.Aplicacao.Clientes.Gateways;
using Oficina.Dominio.Clientes;

namespace Oficina.Aplicacao.Clientes;

public class AdicionarVeiculoUseCase
{
    private readonly IClienteGateway _gateway;
    public AdicionarVeiculoUseCase(IClienteGateway gateway) => _gateway = gateway;

    public async Task<Veiculo?> ExecutarAsync(Guid clienteId, AdicionarVeiculoRequest req, CancellationToken ct)
    {
        var cliente = await _gateway.ObterPorIdAsync(clienteId, ct);
        if (cliente is null) return null;

        var veiculo = Veiculo.Criar(Placa.Criar(req.Placa), req.Marca, req.Modelo, req.Ano);
        cliente.AdicionarVeiculo(veiculo);
        _gateway.MarcarVeiculoComoNovo(veiculo);

        await _gateway.SalvarAsync(ct);
        return veiculo;
    }
}
