using Oficina.Aplicacao.Clientes.Dtos;
using Oficina.Aplicacao.Clientes.Gateways;
using Oficina.Dominio.Clientes;

namespace Oficina.Aplicacao.Clientes;

// Response DTO consumido pelo Presenter e pelo cliente HTTP (mantido neste namespace por compatibilidade).
public sealed record PaginaClientes(IReadOnlyList<ClienteResponse> Itens, int Total, int Pagina, int TamanhoPagina);

// Resultado do use case em termos de entidades de domínio (o Presenter converte em PaginaClientes).
public sealed record ResultadoListaClientes(IReadOnlyList<Cliente> Itens, int Total, int Pagina, int TamanhoPagina);

public class ListarClientesUseCase
{
    private readonly IClienteGateway _gateway;
    public ListarClientesUseCase(IClienteGateway gateway) => _gateway = gateway;

    public async Task<ResultadoListaClientes> ExecutarAsync(int pagina, int tamanho, CancellationToken ct)
    {
        var clientes = await _gateway.ListarAsync(pagina, tamanho, ct);
        var total = await _gateway.ContarAsync(ct);
        return new ResultadoListaClientes(clientes, total, pagina, tamanho);
    }
}
