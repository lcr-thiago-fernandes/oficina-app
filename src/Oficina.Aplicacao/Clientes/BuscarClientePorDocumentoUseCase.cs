using Oficina.Aplicacao.Clientes.Gateways;
using Oficina.Dominio.Clientes;

namespace Oficina.Aplicacao.Clientes;

public class BuscarClientePorDocumentoUseCase
{
    private readonly IClienteGateway _gateway;
    public BuscarClientePorDocumentoUseCase(IClienteGateway gateway) => _gateway = gateway;

    public async Task<Cliente?> ExecutarAsync(string documentoBruto, CancellationToken ct)
    {
        var doc = Documento.Criar(documentoBruto);
        return await _gateway.ObterPorDocumentoAsync(doc, ct);
    }
}
