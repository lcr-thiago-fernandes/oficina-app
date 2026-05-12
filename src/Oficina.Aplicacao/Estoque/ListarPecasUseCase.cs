using Oficina.Aplicacao.Estoque.Dtos;
using Oficina.Dominio.Estoque;

namespace Oficina.Aplicacao.Estoque;

public sealed record PaginaPecas(IReadOnlyList<PecaResponse> Itens, int Total, int Pagina, int TamanhoPagina);

public class ListarPecasUseCase
{
    private readonly IPecaRepositorio _repo;
    public ListarPecasUseCase(IPecaRepositorio repo) => _repo = repo;

    public async Task<PaginaPecas> ExecutarAsync(string? nome, int pagina, int tamanho, bool incluirInativos, CancellationToken ct)
    {
        var lista = await _repo.ListarAsync(nome, pagina, tamanho, incluirInativos, ct);
        var total = await _repo.ContarAsync(nome, incluirInativos, ct);
        return new PaginaPecas(lista.Select(MapeadorEstoque.Mapear).ToList(), total, pagina, tamanho);
    }
}
