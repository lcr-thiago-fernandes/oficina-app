using Oficina.Aplicacao.Clientes.Dtos;
using Oficina.Dominio.Clientes;

namespace Oficina.Aplicacao.Clientes;

public class ListarVeiculosUseCase
{
    private readonly IClienteRepositorio _repo;
    public ListarVeiculosUseCase(IClienteRepositorio repo) => _repo = repo;

    public async Task<IReadOnlyList<VeiculoResponse>?> ExecutarAsync(Guid clienteId, CancellationToken ct)
    {
        var cliente = await _repo.ObterPorIdAsync(clienteId, ct);
        if (cliente is null) return null;
        return cliente.Veiculos.Select(MapeadorClienteResponse.MapearVeiculo).ToList();
    }
}
