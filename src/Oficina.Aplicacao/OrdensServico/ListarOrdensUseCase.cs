using Oficina.Aplicacao.OrdensServico.Dtos;
using Oficina.Aplicacao.OrdensServico.Gateways;
using Oficina.Dominio.OrdensServico;

namespace Oficina.Aplicacao.OrdensServico;

// Response DTO consumido pelo Presenter e pelo cliente HTTP (mantido neste namespace por compatibilidade).
public sealed record PaginaOrdens(IReadOnlyList<OrdemResponse> Itens, int Total, int Pagina, int TamanhoPagina);

// Resultado do use case em termos de entidades de domínio (o Presenter converte em PaginaOrdens).
public sealed record ResultadoListaOrdens(IReadOnlyList<OrdemDeServico> Itens, int Total, int Pagina, int TamanhoPagina);

public class ListarOrdensUseCase
{
    private readonly IOrdemDeServicoGateway _gateway;
    public ListarOrdensUseCase(IOrdemDeServicoGateway gateway) => _gateway = gateway;

    public async Task<ResultadoListaOrdens> ExecutarAsync(string? statusFiltro, int pagina, int tamanho, CancellationToken ct)
    {
        StatusOrdemDeServico? status = null;
        if (!string.IsNullOrWhiteSpace(statusFiltro))
        {
            if (!Enum.TryParse<StatusOrdemDeServico>(statusFiltro, true, out var s))
                throw new OrdemInvalidaException($"Status '{statusFiltro}' inválido.");
            status = s;
        }

        var lista = await _gateway.ListarAsync(status, pagina, tamanho, ct);
        var total = await _gateway.ContarAsync(status, ct);
        return new ResultadoListaOrdens(lista, total, pagina, tamanho);
    }
}
