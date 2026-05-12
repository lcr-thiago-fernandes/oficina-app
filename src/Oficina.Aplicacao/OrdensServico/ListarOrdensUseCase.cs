using Oficina.Aplicacao.OrdensServico.Dtos;
using Oficina.Dominio.OrdensServico;

namespace Oficina.Aplicacao.OrdensServico;

public sealed record PaginaOrdens(IReadOnlyList<OrdemResponse> Itens, int Total, int Pagina, int TamanhoPagina);

public class ListarOrdensUseCase
{
    private readonly IOrdemDeServicoRepositorio _repo;
    public ListarOrdensUseCase(IOrdemDeServicoRepositorio repo) => _repo = repo;

    public async Task<PaginaOrdens> ExecutarAsync(string? statusFiltro, int pagina, int tamanho, CancellationToken ct)
    {
        StatusOrdemDeServico? status = null;
        if (!string.IsNullOrWhiteSpace(statusFiltro))
        {
            if (!Enum.TryParse<StatusOrdemDeServico>(statusFiltro, true, out var s))
                throw new OrdemInvalidaException($"Status '{statusFiltro}' inválido.");
            status = s;
        }

        var lista = await _repo.ListarAsync(status, pagina, tamanho, ct);
        var total = await _repo.ContarAsync(status, ct);
        return new PaginaOrdens(lista.Select(MapeadorOrdem.Mapear).ToList(), total, pagina, tamanho);
    }
}
