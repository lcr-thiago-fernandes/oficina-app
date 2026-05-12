using Oficina.Dominio.Clientes;

namespace Oficina.Aplicacao.Clientes;

public class RemoverClienteUseCase
{
    private readonly IClienteRepositorio _repo;
    public RemoverClienteUseCase(IClienteRepositorio repo) => _repo = repo;

    public async Task<bool> ExecutarAsync(Guid id, CancellationToken ct)
    {
        var cliente = await _repo.ObterPorIdAsync(id, ct);
        if (cliente is null) return false;

        cliente.Inativar(); // soft delete
        await _repo.SalvarAsync(ct);
        return true;
    }
}
