using Oficina.Aplicacao.Estoque.Dtos;
using Oficina.Aplicacao.Estoque.Gateways;
using Oficina.Dominio.Estoque;

namespace Oficina.Aplicacao.Estoque;

public class AtualizarPecaUseCase
{
    private readonly IPecaGateway _gateway;
    public AtualizarPecaUseCase(IPecaGateway gateway) => _gateway = gateway;

    public async Task<Peca?> ExecutarAsync(Guid id, AtualizarPecaRequest req, CancellationToken ct)
    {
        var p = await _gateway.ObterPorIdAsync(id, ct);
        if (p is null) return null;

        p.AtualizarDados(req.Nome, req.PrecoUnitario);
        await _gateway.SalvarAsync(ct);
        return p;
    }
}
