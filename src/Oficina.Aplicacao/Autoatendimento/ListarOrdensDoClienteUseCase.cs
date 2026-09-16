using Oficina.Aplicacao.Autoatendimento.Dtos;
using Oficina.Aplicacao.Clientes.Gateways;
using Oficina.Aplicacao.OrdensServico.Gateways;
using Oficina.Dominio.Clientes;

namespace Oficina.Aplicacao.Autoatendimento;

/// <summary>
/// Ordens do cliente identificado no token. O documento vem da claim, nunca
/// do corpo ou da query — é isso que impede um cliente de ler a OS de outro.
/// </summary>
public class ListarOrdensDoClienteUseCase
{
    private readonly IOrdemDeServicoGateway _ordens;
    private readonly IClienteGateway _clientes;

    public ListarOrdensDoClienteUseCase(IOrdemDeServicoGateway ordens, IClienteGateway clientes)
    {
        _ordens = ordens;
        _clientes = clientes;
    }

    /// <returns>Lista de ordens, ou <c>null</c> se o documento for inválido ou o cliente não existir.</returns>
    public async Task<IReadOnlyList<OrdemResumoResponse>?> ExecutarAsync(
        string documentoBruto, CancellationToken ct)
    {
        Documento documento;
        try { documento = Documento.Criar(documentoBruto); }
        catch (DocumentoInvalidoException) { return null; }

        var cliente = await _clientes.ObterPorDocumentoAsync(documento, ct);
        if (cliente is null) return null;

        var ordens = await _ordens.ListarPorClienteAsync(cliente.Id, ct);

        return ordens
            .Select(o => new OrdemResumoResponse(
                o.Numero, o.Status.ToString(), o.Unidade, o.TotalGeral, o.CriadaEm))
            .ToList();
    }
}
