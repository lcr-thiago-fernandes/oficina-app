using Oficina.Aplicacao.OrdensServico.Dtos;
using Oficina.Dominio.Estoque;
using Oficina.Dominio.OrdensServico;

namespace Oficina.Aplicacao.OrdensServico;

public class IniciarExecucaoUseCase
{
    private readonly IOrdemDeServicoRepositorio _ordens;
    private readonly IPecaRepositorio _pecas;

    public IniciarExecucaoUseCase(IOrdemDeServicoRepositorio ordens, IPecaRepositorio pecas)
    {
        _ordens = ordens;
        _pecas = pecas;
    }

    public async Task<OrdemResponse?> ExecutarAsync(Guid ordemId, CancellationToken ct)
    {
        OrdemResponse? resposta = null;

        await _ordens.EmTransacaoSerializadaAsync(async tx =>
        {
            var ordem = await _ordens.ObterPorIdAsync(ordemId, tx);
            if (ordem is null) return;

            // 1) muda estado da OS — pode lançar OrcamentoNaoAprovadoException ou TransicaoInvalida
            ordem.IniciarExecucao();

            // 2) baixar estoque para cada item de peça (mesma transação)
            foreach (var item in ordem.ItensPeca)
            {
                var peca = await _pecas.ObterPorIdAsync(item.PecaId, tx)
                    ?? throw new OrdemInvalidaException(
                        $"Peça {item.PecaId} referenciada na OS não existe mais.");

                peca.RegistrarSaida(
                    quantidade: item.Quantidade,
                    motivo: $"OS #{ordem.Numero}",
                    ordemServicoId: ordem.Id);
            }

            // 3) persiste tudo na mesma transação
            await _ordens.SalvarAsync(tx);
            ordem.LimparEventos();

            resposta = MapeadorOrdem.Mapear(ordem);
        }, ct);

        return resposta;
    }
}
