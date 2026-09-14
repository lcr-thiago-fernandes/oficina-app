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

        // Referência à OS carregada, visível FORA da transação: é o que permite
        // publicar o evento de falha quando quem lança é o CommitAsync — que
        // acontece depois que o delegate retorna, dentro de
        // EmTransacaoSerializadaAsync. Um conflito de serialização do Postgres
        // (40001) ou uma queda de conexão no commit reverte tudo e, antes desta
        // correção, não produzia evento nenhum: nem sucesso (publicado só
        // depois) nem falha (o try/catch ficava no escopo interno).
        OrdemDeServico? carregada = null;

        try
        {
            await _ordens.EmTransacaoSerializadaAsync(async tx =>
            {
                var ordem = await _ordens.ObterPorIdAsync(ordemId, tx);
                if (ordem is null) return;
                carregada = ordem;

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
        }
        // O try envolve a chamada inteira (delegate + commit + rollback), então
        // há exatamente um ponto que publica falha para este caso de uso — sem
        // risco de evento duplicado e sem o buraco do commit. O evento de
        // SUCESSO continua fora, depois do commit: publicá-lo aqui dentro
        // anunciaria uma transação que ainda pode ser revertida.
        catch (Exception ex) when (carregada is not null
                                   && PublicadorEventoOsExtensions.EhFalhaDeProcessamento(ex))
        {
            _publicador.Publicar(
                EventoOrdemServico.DeFalha(carregada.Numero, carregada.Status.ToString(), carregada.Unidade));
            throw;
        }

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
