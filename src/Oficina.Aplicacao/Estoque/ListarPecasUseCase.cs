using Oficina.Aplicacao.Estoque.Dtos;
using Oficina.Aplicacao.Estoque.Gateways;
using Oficina.Dominio.Estoque;

namespace Oficina.Aplicacao.Estoque;

// Response DTO consumido pelo Presenter e pelo cliente HTTP (mantido neste namespace por compatibilidade).
public sealed record PaginaPecas(IReadOnlyList<PecaResponse> Itens, int Total, int Pagina, int TamanhoPagina);

// Resultado do use case em termos de entidades de domínio (o Presenter converte em PaginaPecas).
public sealed record ResultadoListaPecas(IReadOnlyList<Peca> Itens, int Total, int Pagina, int TamanhoPagina);

public class ListarPecasUseCase
{
    private readonly IPecaGateway _gateway;
    public ListarPecasUseCase(IPecaGateway gateway) => _gateway = gateway;

    public async Task<ResultadoListaPecas> ExecutarAsync(string? nome, int pagina, int tamanho, bool incluirInativos, CancellationToken ct)
    {
        var lista = await _gateway.ListarAsync(nome, pagina, tamanho, incluirInativos, ct);
        var total = await _gateway.ContarAsync(nome, incluirInativos, ct);
        return new ResultadoListaPecas(lista, total, pagina, tamanho);
    }
}
