using Oficina.Aplicacao.Catalogo.Dtos;
using Oficina.Dominio.Catalogo;

namespace Oficina.Aplicacao.Catalogo;

public sealed record PaginaServicos(IReadOnlyList<ServicoResponse> Itens, int Total, int Pagina, int TamanhoPagina);

public class ListarServicosUseCase
{
    private readonly IServicoRepositorio _repo;
    public ListarServicosUseCase(IServicoRepositorio repo) => _repo = repo;

    public async Task<PaginaServicos> ExecutarAsync(string? filtroNome, int pagina, int tamanho, bool incluirInativos, CancellationToken ct)
    {
        var lista = await _repo.ListarAsync(filtroNome, pagina, tamanho, incluirInativos, ct);
        var total = await _repo.ContarAsync(filtroNome, incluirInativos, ct);
        return new PaginaServicos(
            lista.Select(MapeadorServicoResponse.Mapear).ToList(),
            total, pagina, tamanho);
    }
}
