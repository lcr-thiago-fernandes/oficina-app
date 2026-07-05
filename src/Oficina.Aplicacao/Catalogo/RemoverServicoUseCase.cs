using Oficina.Aplicacao.Catalogo.Gateways;

namespace Oficina.Aplicacao.Catalogo;

public class RemoverServicoUseCase
{
    private readonly IServicoGateway _gateway;
    public RemoverServicoUseCase(IServicoGateway gateway) => _gateway = gateway;

    public async Task<bool> ExecutarAsync(Guid id, CancellationToken ct)
    {
        var servico = await _gateway.ObterPorIdAsync(id, ct);
        if (servico is null) return false;

        servico.Inativar();
        await _gateway.SalvarAsync(ct);
        return true;
    }
}
