using Oficina.Aplicacao.Clientes.Dtos;
using Oficina.Dominio.Clientes;

namespace Oficina.Aplicacao.Clientes;

public class AtualizarClienteUseCase
{
    private readonly IClienteRepositorio _repo;
    public AtualizarClienteUseCase(IClienteRepositorio repo) => _repo = repo;

    public async Task<ClienteResponse?> ExecutarAsync(Guid id, AtualizarClienteRequest req, CancellationToken ct)
    {
        var cliente = await _repo.ObterPorIdAsync(id, ct);
        if (cliente is null) return null;

        cliente.AtualizarContato(
            req.Nome,
            Email.Criar(req.Email),
            Telefone.Criar(req.Telefone));

        await _repo.SalvarAsync(ct);
        return MapeadorClienteResponse.Mapear(cliente);
    }
}
