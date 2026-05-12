using Oficina.Aplicacao.Clientes.Dtos;
using Oficina.Dominio.Clientes;

namespace Oficina.Aplicacao.Clientes;

public sealed record PaginaClientes(IReadOnlyList<ClienteResponse> Itens, int Total, int Pagina, int TamanhoPagina);

public class ListarClientesUseCase
{
    private readonly IClienteRepositorio _repo;
    public ListarClientesUseCase(IClienteRepositorio repo) => _repo = repo;

    public async Task<PaginaClientes> ExecutarAsync(int pagina, int tamanho, CancellationToken ct)
    {
        var clientes = await _repo.ListarAsync(pagina, tamanho, ct);
        var total = await _repo.ContarAsync(ct);
        return new PaginaClientes(
            clientes.Select(MapeadorClienteResponse.Mapear).ToList(),
            total, pagina, tamanho);
    }
}
