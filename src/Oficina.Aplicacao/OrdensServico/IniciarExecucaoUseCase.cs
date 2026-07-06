using Oficina.Aplicacao.Estoque.Gateways;
using Oficina.Aplicacao.OrdensServico.Gateways;
using Oficina.Dominio.OrdensServico;

namespace Oficina.Aplicacao.OrdensServico;

public class IniciarExecucaoUseCase
{
    private readonly IOrdemDeServicoGateway _ordens;
    private readonly IPecaGateway _pecas;
    private readonly INotificacaoGateway _notificacoes;

    public IniciarExecucaoUseCase(
        IOrdemDeServicoGateway ordens, IPecaGateway pecas, INotificacaoGateway notificacoes)
    {
        _ordens = ordens;
        _pecas = pecas;
        _notificacoes = notificacoes;
    }

    public async Task<OrdemDeServico?> ExecutarAsync(Guid ordemId, CancellationToken ct)
    {
        OrdemDeServico? resultado = null;

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

                var mov = peca.RegistrarSaida(
                    quantidade: item.Quantidade,
                    motivo: $"OS #{ordem.Numero}",
                    ordemServicoId: ordem.Id);
                _pecas.MarcarMovimentacaoComoNova(mov);
            }

            // 3) persiste tudo na mesma transação
            await _ordens.SalvarAsync(tx);

            resultado = ordem;
        }, ct);

        // 4) notifica fora da transação serializável (efeito colateral não deve
        //    prender a transação nem provocar rollback se o "envio" falhar)
        if (resultado is not null)
            await _notificacoes.NotificarMudancaDeStatusAsync(resultado, ct);

        return resultado;
    }
}
