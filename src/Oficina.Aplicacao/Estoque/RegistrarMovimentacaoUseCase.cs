using Oficina.Aplicacao.Estoque.Dtos;
using Oficina.Dominio.Estoque;

namespace Oficina.Aplicacao.Estoque;

public class RegistrarMovimentacaoUseCase
{
    private readonly IPecaRepositorio _repo;
    public RegistrarMovimentacaoUseCase(IPecaRepositorio repo) => _repo = repo;

    public async Task<MovimentacaoResponse?> ExecutarAsync(
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

        MovimentacaoResponse? resp = null;

        await _repo.EmTransacaoSerializadaAsync(async tx =>
        {
            var peca = await _repo.ObterPorIdAsync(pecaId, tx);
            if (peca is null) return;

            var mov = tipo == TipoMovimentacao.Entrada
                ? peca.RegistrarEntrada(req.Quantidade, req.Motivo)
                : peca.RegistrarSaida(req.Quantidade, req.Motivo, req.OrdemServicoId);
            _repo.MarcarMovimentacaoComoNova(mov);

            await _repo.SalvarAsync(tx);
            resp = MapeadorEstoque.MapearMov(mov);
        }, ct);

        return resp;
    }
}
