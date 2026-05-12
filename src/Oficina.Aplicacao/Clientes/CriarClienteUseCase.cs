using Oficina.Aplicacao.Clientes.Dtos;
using Oficina.Dominio.Clientes;

namespace Oficina.Aplicacao.Clientes;

public class CriarClienteUseCase
{
    private readonly IClienteRepositorio _repo;

    public CriarClienteUseCase(IClienteRepositorio repo) => _repo = repo;

    public async Task<ClienteResponse> ExecutarAsync(CriarClienteRequest req, CancellationToken ct)
    {
        var documento = Documento.Criar(req.Documento);

        if (await _repo.ExisteDocumentoAsync(documento, ct))
            throw new DocumentoJaCadastradoException(documento.Valor);

        var cliente = Cliente.Criar(
            req.Nome,
            documento,
            Email.Criar(req.Email),
            Telefone.Criar(req.Telefone));

        await _repo.AdicionarAsync(cliente, ct);
        await _repo.SalvarAsync(ct);

        return MapeadorClienteResponse.Mapear(cliente);
    }
}
