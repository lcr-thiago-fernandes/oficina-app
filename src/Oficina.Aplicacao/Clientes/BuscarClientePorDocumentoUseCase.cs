using Oficina.Aplicacao.Clientes.Dtos;
using Oficina.Dominio.Clientes;

namespace Oficina.Aplicacao.Clientes;

public class BuscarClientePorDocumentoUseCase
{
    private readonly IClienteRepositorio _repo;
    public BuscarClientePorDocumentoUseCase(IClienteRepositorio repo) => _repo = repo;

    public async Task<ClienteResponse?> ExecutarAsync(string documentoBruto, CancellationToken ct)
    {
        var doc = Documento.Criar(documentoBruto);
        var cliente = await _repo.ObterPorDocumentoAsync(doc, ct);
        return cliente is null ? null : MapeadorClienteResponse.Mapear(cliente);
    }
}
