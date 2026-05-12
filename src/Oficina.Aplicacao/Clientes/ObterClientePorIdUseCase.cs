using Oficina.Aplicacao.Clientes.Dtos;
using Oficina.Dominio.Clientes;

namespace Oficina.Aplicacao.Clientes;

public class ObterClientePorIdUseCase
{
    private readonly IClienteRepositorio _repo;
    public ObterClientePorIdUseCase(IClienteRepositorio repo) => _repo = repo;

    public async Task<ClienteResponse?> ExecutarAsync(Guid id, CancellationToken ct)
    {
        var cliente = await _repo.ObterPorIdAsync(id, ct);
        return cliente is null ? null : MapeadorClienteResponse.Mapear(cliente);
    }
}
