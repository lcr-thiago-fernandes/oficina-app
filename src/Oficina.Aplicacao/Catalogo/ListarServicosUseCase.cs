using Oficina.Aplicacao.Catalogo.Dtos;
using Oficina.Aplicacao.Catalogo.Gateways;
using Oficina.Dominio.Catalogo;

namespace Oficina.Aplicacao.Catalogo;

// Response DTO consumido pelo Presenter e pelo cliente HTTP (mantido neste namespace por compatibilidade dos testes).
public sealed record PaginaServicos(IReadOnlyList<ServicoResponse> Itens, int Total, int Pagina, int TamanhoPagina);

// Resultado do use case em termos de entidades de domínio (o Presenter converte em PaginaServicos).
public sealed record ResultadoListaServicos(IReadOnlyList<Servico> Itens, int Total, int Pagina, int TamanhoPagina);

public class ListarServicosUseCase
{
    private readonly IServicoGateway _gateway;
    public ListarServicosUseCase(IServicoGateway gateway) => _gateway = gateway;

    public async Task<ResultadoListaServicos> ExecutarAsync(string? filtroNome, int pagina, int tamanho, bool incluirInativos, CancellationToken ct)
    {
        var lista = await _gateway.ListarAsync(filtroNome, pagina, tamanho, incluirInativos, ct);
        var total = await _gateway.ContarAsync(filtroNome, incluirInativos, ct);
        return new ResultadoListaServicos(lista, total, pagina, tamanho);
    }
}
