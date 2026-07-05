using Oficina.Aplicacao.Clientes.Dtos;
using Oficina.Aplicacao.Clientes.Gateways;
using Oficina.Dominio.Clientes;

namespace Oficina.Aplicacao.Clientes;

public class AtualizarVeiculoUseCase
{
    private readonly IClienteGateway _gateway;
    public AtualizarVeiculoUseCase(IClienteGateway gateway) => _gateway = gateway;

    public async Task<Veiculo?> ExecutarAsync(Guid clienteId, string placaBruta, AtualizarVeiculoRequest req, CancellationToken ct)
    {
        var cliente = await _gateway.ObterPorIdAsync(clienteId, ct);
        if (cliente is null) return null;

        var placa = Placa.Criar(placaBruta);
        cliente.AtualizarVeiculo(placa, req.Marca, req.Modelo, req.Ano);

        await _gateway.SalvarAsync(ct);
        return cliente.Veiculos.First(x => x.Placa.Equals(placa));
    }
}
