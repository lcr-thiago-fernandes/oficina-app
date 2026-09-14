using Oficina.Aplicacao.Estoque.Gateways;
using Oficina.Aplicacao.OrdensServico.Gateways;
using Oficina.Aplicacao.OrdensServico.Telemetria;
using Oficina.Dominio.OrdensServico;

namespace Oficina.Aplicacao.OrdensServico;

public class IniciarExecucaoUseCase
{
    private readonly IOrdemDeServicoGateway _ordens;
    private readonly IPecaGateway _pecas;
    private readonly INotificacaoGateway _notificacoes;
    private readonly IPublicadorEventoOs _publicador;

    public IniciarExecucaoUseCase(
        IOrdemDeServicoGateway ordens, IPecaGateway pecas, INotificacaoGateway notificacoes,
        IPublicadorEventoOs publicador)
    {
        _ordens = ordens;
        _pecas = pecas;
        _notificacoes = notificacoes;
        _publicador = publicador;
    }

    public async Task<OrdemDeServico?> ExecutarAsync(Guid ordemId, CancellationToken ct)
    {
        OrdemDeServico? resultado = null;

        await _ordens.EmTransacaoSerializadaAsync(async tx =>
        {
            var ordem = await _ordens.ObterPorIdAsync(ordemId, tx);
            if (ordem is null) return;

            // publicarSucesso: false — o commit ainda acontece depois que este
            // delegate retorna (EmTransacaoSerializadaAsync só chama CommitAsync
            // após "acao" completar); publicar sucesso aqui dentro anunciaria uma
            // transação que pode ainda ser revertida no commit (ex.: conflito de
            // serialização, Postgres 40001). O evento de falha, por outro lado,
            // é seguro aqui: qualquer exceção nesta função impede o commit de
            // qualquer forma.
            await _publicador.ExecutarTransicaoComTelemetriaAsync(ordem, async () =>
            {
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
            }, publicarSucesso: false);
        }, ct);

        // 4) publica o sucesso e notifica só depois que a transação foi commitada
        //    (efeito colateral não deve prender a transação nem provocar rollback
        //    se o "envio" falhar).
        if (resultado is not null)
        {
            _publicador.Publicar(EventoOrdemServico.DeUltimaTransicao(resultado));
            await _notificacoes.NotificarMudancaDeStatusAsync(resultado, ct);
        }

        return resultado;
    }
}
