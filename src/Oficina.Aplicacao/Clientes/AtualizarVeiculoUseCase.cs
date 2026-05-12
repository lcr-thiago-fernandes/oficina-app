using Oficina.Aplicacao.Clientes.Dtos;
using Oficina.Dominio.Clientes;

namespace Oficina.Aplicacao.Clientes;

public class AtualizarVeiculoUseCase
{
    private readonly IClienteRepositorio _repo;
    public AtualizarVeiculoUseCase(IClienteRepositorio repo) => _repo = repo;

    public async Task<VeiculoResponse?> ExecutarAsync(Guid clienteId, string placaBruta, AtualizarVeiculoRequest req, CancellationToken ct)
    {
        var cliente = await _repo.ObterPorIdAsync(clienteId, ct);
        if (cliente is null) return null;

        var placa = Placa.Criar(placaBruta);
        cliente.AtualizarVeiculo(placa, req.Marca, req.Modelo, req.Ano);

        await _repo.SalvarAsync(ct);
        var v = cliente.Veiculos.First(x => x.Placa.Equals(placa));
        return MapeadorClienteResponse.MapearVeiculo(v);
    }
}
