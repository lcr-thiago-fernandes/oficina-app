using Oficina.Aplicacao.Estoque.Dtos;
using Oficina.Aplicacao.Estoque.Gateways;
using Oficina.Dominio.Estoque;

namespace Oficina.Aplicacao.Estoque;

public class RegistrarMovimentacaoUseCase
{
    private readonly IPecaGateway _gateway;
    public RegistrarMovimentacaoUseCase(IPecaGateway gateway) => _gateway = gateway;

    public async Task<MovimentacaoEstoque?> ExecutarAsync(
        Guid pecaId,
        RegistrarMovimentacaoRequest req,
        CancellationToken ct)
    {
        var tipo = req.Tipo switch
        {
            "Entrada" => TipoMovimentacao.Entrada,
            "Saida" => TipoMovimentacao.Saida,
            _ => throw new MovimentacaoInvalidaException(
                "Tipo de movimentação inválido. Use 'Entrada' ou 'Saida'.")
        };

        MovimentacaoEstoque? mov = null;

        await _gateway.EmTransacaoSerializadaAsync(async tx =>
        {
            var peca = await _gateway.ObterPorIdAsync(pecaId, tx);
            if (peca is null) return;

            mov = tipo == TipoMovimentacao.Entrada
                ? peca.RegistrarEntrada(req.Quantidade, req.Motivo)
                : peca.RegistrarSaida(req.Quantidade, req.Motivo, req.OrdemServicoId);
            _gateway.MarcarMovimentacaoComoNova(mov);

            await _gateway.SalvarAsync(tx);
        }, ct);

        return mov;
    }
}
