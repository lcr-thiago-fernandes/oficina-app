using Oficina.Aplicacao.Clientes.Dtos;
using Oficina.Aplicacao.Clientes.Gateways;
using Oficina.Dominio.Clientes;

namespace Oficina.Aplicacao.Clientes;

public class CriarClienteUseCase
{
    private readonly IClienteGateway _gateway;

    public CriarClienteUseCase(IClienteGateway gateway) => _gateway = gateway;

    public async Task<Cliente> ExecutarAsync(CriarClienteRequest req, CancellationToken ct)
    {
        var documento = Documento.Criar(req.Documento);

        if (await _gateway.ExisteDocumentoAsync(documento, ct))
            throw new DocumentoJaCadastradoException(documento.Valor);

        var cliente = Cliente.Criar(
            req.Nome,
            documento,
            Email.Criar(req.Email),
            Telefone.Criar(req.Telefone));

        await _gateway.AdicionarAsync(cliente, ct);
        await _gateway.SalvarAsync(ct);

        return cliente;
    }
}
