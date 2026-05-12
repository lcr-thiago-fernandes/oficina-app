using Oficina.Dominio.Clientes;

namespace Oficina.Aplicacao.Clientes;

public class RemoverVeiculoUseCase
{
    private readonly IClienteRepositorio _repo;
    public RemoverVeiculoUseCase(IClienteRepositorio repo) => _repo = repo;

    public async Task<bool> ExecutarAsync(Guid clienteId, string placaBruta, CancellationToken ct)
    {
        var cliente = await _repo.ObterPorIdAsync(clienteId, ct);
        if (cliente is null) return false;

        cliente.RemoverVeiculo(Placa.Criar(placaBruta));
        await _repo.SalvarAsync(ct);
        return true;
    }
}
