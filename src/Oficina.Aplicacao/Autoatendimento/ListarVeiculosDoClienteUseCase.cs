using Oficina.Aplicacao.Clientes.Dtos;
using Oficina.Aplicacao.Clientes.Gateways;
using Oficina.Dominio.Clientes;

namespace Oficina.Aplicacao.Autoatendimento;

/// <summary>
/// Veículos do cliente identificado no token. O documento vem da claim, nunca
/// do corpo ou da query — é isso que impede um cliente de ver o veículo de outro.
/// </summary>
public class ListarVeiculosDoClienteUseCase
{
    private readonly IClienteGateway _clientes;

    public ListarVeiculosDoClienteUseCase(IClienteGateway clientes) => _clientes = clientes;

    /// <returns>Lista de veículos, ou <c>null</c> se o documento for inválido ou o cliente não existir.</returns>
    public async Task<IReadOnlyList<VeiculoResponse>?> ExecutarAsync(
        string documentoBruto, CancellationToken ct)
    {
        Documento documento;
        try { documento = Documento.Criar(documentoBruto); }
        catch (DocumentoInvalidoException) { return null; }

        var cliente = await _clientes.ObterPorDocumentoAsync(documento, ct);
        if (cliente is null) return null;

        return cliente.Veiculos
            .Select(v => new VeiculoResponse(v.Id, v.Placa.Valor, v.Marca, v.Modelo, v.Ano))
            .ToList();
    }
}
