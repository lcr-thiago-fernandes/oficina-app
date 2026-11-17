using Microsoft.Extensions.Logging;
using Oficina.Aplicacao.Clientes.Gateways;
using Oficina.Aplicacao.Estoque.Gateways;
using Oficina.Aplicacao.OrdensServico.Gateways;
using Oficina.Aplicacao.OrdensServico.Saga.Dtos;
using Oficina.Aplicacao.OrdensServico.Telemetria;
using Oficina.Dominio.Estoque;
using Oficina.Dominio.OrdensServico;

namespace Oficina.Aplicacao.OrdensServico.Saga;

/// <summary>
/// Efeitos locais dos passos da saga. Roda dentro do consumo da mensagem (a transação é a do
/// outbox do consumidor), por isso não abre transação própria: só muta o change tracker e chama
/// <c>SalvarAsync</c> uma vez por passo efetivo. Os gateways compartilham o mesmo DbContext scoped.
/// </summary>
public sealed class PassosSagaOrdemServico : IPassosSagaOrdemServico
{
    private readonly IOrdemDeServicoGateway _ordens;
    private readonly IPecaGateway _pecas;
    private readonly IClienteGateway _clientes;
    private readonly INotificacaoGateway _notificacoes;
    private readonly IPublicadorEventoOs _publicador;
    private readonly ILogger<PassosSagaOrdemServico> _log;

    public PassosSagaOrdemServico(
        IOrdemDeServicoGateway ordens,
        IPecaGateway pecas,
        IClienteGateway clientes,
        INotificacaoGateway notificacoes,
        IPublicadorEventoOs publicador,
        ILogger<PassosSagaOrdemServico> log)
    {
        _ordens = ordens;
        _pecas = pecas;
        _clientes = clientes;
        _notificacoes = notificacoes;
        _publicador = publicador;
        _log = log;
    }

    public async Task<PedidoOrcamento> MontarPedidoOrcamentoAsync(Guid ordemServicoId, CancellationToken ct)
    {
        var os = await ObterOsAsync(ordemServicoId, ct);
        var cliente = await _clientes.ObterPorIdAsync(os.ClienteId, ct)
            ?? throw new InvalidOperationException($"Cliente {os.ClienteId} da OS {os.Id} não encontrado.");

        var itens = os.ItensServico
            .Select(s => new ItemPedidoOrcamento(TipoItemPedido.Servico, s.ServicoId, s.ServicoNome, s.Quantidade, s.PrecoSnapshot))
            .Concat(os.ItensPeca
                .Select(p => new ItemPedidoOrcamento(TipoItemPedido.Peca, p.PecaId, p.PecaNome, p.Quantidade, p.PrecoSnapshot)))
            .ToList();

        return new PedidoOrcamento(os.Numero, cliente.Documento.Valor, cliente.Nome, cliente.Email.Valor, itens);
    }

    public Task RegistrarOrcamentoGeradoAsync(Guid ordemServicoId, CancellationToken ct) =>
        AplicarTransicaoAsync(ordemServicoId, StatusOrdemDeServico.AguardandoAprovacao, os => os.AguardarAprovacao(), ct);

    public Task RegistrarOrcamentoAprovadoAsync(Guid ordemServicoId, CancellationToken ct) =>
        AplicarTransicaoAsync(ordemServicoId, StatusOrdemDeServico.AguardandoPagamento, os => os.AguardarPagamento(), ct);

    public Task RegistrarOrcamentoRejeitadoAsync(Guid ordemServicoId, string? motivo, CancellationToken ct) =>
        AplicarTransicaoAsync(ordemServicoId, StatusOrdemDeServico.Cancelada, os => os.Rejeitar(motivo), ct,
            efeitoNoEstoque: os => DesfazerEstoqueAsync(os, ct));

    public async Task<PedidoExecucao?> ConfirmarPagamentoAsync(Guid ordemServicoId, CancellationToken ct)
    {
        var os = await ObterOsAsync(ordemServicoId, ct);
        if (os.Status == StatusOrdemDeServico.Cancelada)
        {
            _log.LogWarning("Pagamento da OS {OrdemServicoId} confirmado com a OS já cancelada: estoque não baixado.", os.Id);
            return null;
        }

        var pecas = await CarregarPecasAsync(os, ct);

        // Tudo ou nada: se alguma reserva já foi liberada/reposta (cancelamento ou compensação
        // processados antes deste pagamento), nenhuma é baixada e a saga estorna o pagamento.
        var semReserva = pecas
            .Where(p => p.SituacaoReserva(os.Id) is not (SituacaoReservaPeca.Reservada or SituacaoReservaPeca.Confirmada))
            .ToList();
        if (semReserva.Count > 0)
        {
            _log.LogWarning(
                "Pagamento da OS {OrdemServicoId} não pode ser honrado: reserva inexistente/desfeita nas peças {Pecas}.",
                os.Id, string.Join(", ", semReserva.Select(p => $"{p.Sku.Valor} ({p.SituacaoReserva(os.Id)})")));
            return null;
        }

        var houveMovimento = false;
        foreach (var peca in pecas)
            houveMovimento |= Marcar(peca.ConfirmarReserva(os.Id));

        if (houveMovimento)
            await _ordens.SalvarAsync(ct);

        return new PedidoExecucao(
            os.Numero,
            os.Unidade,
            os.ItensServico.Select(s => new ItemPedidoExecucao(s.ServicoNome, s.Quantidade)).ToList(),
            os.ItensPeca.Select(p => new ItemPedidoExecucao(p.PecaNome, p.Quantidade)).ToList());
    }

    public Task RegistrarExecucaoEnfileiradaAsync(Guid ordemServicoId, CancellationToken ct) =>
        AplicarTransicaoAsync(ordemServicoId, StatusOrdemDeServico.EmFilaExecucao, os => os.EnfileirarExecucao(), ct);

    public Task RegistrarStatusExecucaoAsync(Guid ordemServicoId, EtapaExecucao etapa, CancellationToken ct) =>
        etapa switch
        {
            EtapaExecucao.EmDiagnostico => AplicarTransicaoAsync(
                ordemServicoId, StatusOrdemDeServico.EmDiagnostico, os => os.IniciarDiagnostico(), ct),
            EtapaExecucao.EmReparo => AplicarTransicaoAsync(
                ordemServicoId, StatusOrdemDeServico.EmReparo, os => os.IniciarReparo(), ct),
            _ => throw new ArgumentOutOfRangeException(nameof(etapa), etapa, "Etapa de execução desconhecida.")
        };

    public Task RegistrarExecucaoFinalizadaAsync(Guid ordemServicoId, CancellationToken ct) =>
        AplicarTransicaoAsync(ordemServicoId, StatusOrdemDeServico.Finalizada, os => os.Finalizar(), ct);

    public async Task CancelarAsync(Guid ordemServicoId, string motivo, CancellationToken ct)
    {
        var os = await ObterOsAsync(ordemServicoId, ct);

        if (os.Status == StatusOrdemDeServico.Cancelada)
        {
            // Compensação reentregue: o domínio devolve null para o que já foi desfeito.
            if (await DesfazerEstoqueAsync(os, ct))
                await _ordens.SalvarAsync(ct);
            return;
        }

        await _publicador.ExecutarTransicaoComTelemetriaAsync(os, async () =>
        {
            // Cancelar antes do estoque: status não cancelável lança sem ter mexido em peça.
            os.Cancelar(motivo);
            await DesfazerEstoqueAsync(os, ct);
            await _ordens.SalvarAsync(ct);
        });

        await _notificacoes.NotificarMudancaDeStatusAsync(os, ct);
    }

    /// <summary>
    /// Bloco único de cada transição da saga: carrega a OS (inexistente → lança), ignora se ela já
    /// está cancelada ou já alcançou <paramref name="destino"/> (mensagem duplicada ou atrasada),
    /// senão aplica a transição (e o efeito no estoque) com telemetria, salva e notifica.
    /// Devolve <c>null</c> quando foi no-op.
    /// </summary>
    private async Task<OrdemDeServico?> AplicarTransicaoAsync(
        Guid id,
        StatusOrdemDeServico destino,
        Action<OrdemDeServico> transicao,
        CancellationToken ct,
        Func<OrdemDeServico, Task>? efeitoNoEstoque = null)
    {
        var os = await ObterOsAsync(id, ct);
        if (os.Status == StatusOrdemDeServico.Cancelada || os.Status.JaAlcancou(destino))
        {
            _log.LogInformation(
                "Passo da saga ignorado para a OS {OrdemServicoId}: status {Status}, destino {Destino}.",
                os.Id, os.Status, destino);
            return null;
        }

        await _publicador.ExecutarTransicaoComTelemetriaAsync(os, async () =>
        {
            transicao(os);
            if (efeitoNoEstoque is not null)
                await efeitoNoEstoque(os);
            await _ordens.SalvarAsync(ct);
        });

        await _notificacoes.NotificarMudancaDeStatusAsync(os, ct);
        return os;
    }

    /// <summary>
    /// Desfaz o estoque da OS por peça: Reservada → libera; Confirmada → repõe; demais → nada.
    /// Devolve <c>true</c> se gerou alguma movimentação.
    /// </summary>
    private async Task<bool> DesfazerEstoqueAsync(OrdemDeServico os, CancellationToken ct)
    {
        var houveMovimento = false;
        foreach (var peca in await CarregarPecasAsync(os, ct))
        {
            var situacao = peca.SituacaoReserva(os.Id);
            if (situacao == SituacaoReservaPeca.Reservada)
            {
                houveMovimento |= Marcar(peca.LiberarReserva(os.Id));
            }
            else if (situacao == SituacaoReservaPeca.Confirmada)
            {
                if (!TemReserva(peca, os.Id))
                {
                    // OS da Fase 3: a saída manual com o OrdemServicoId lê como "Confirmada", mas não
                    // houve reserva — Repor lançaria. A baixa manual não é compensada automaticamente.
                    _log.LogWarning(
                        "OS {OrdemServicoId} sem reserva na peça {Sku} (saída manual legada): estoque não reposto.",
                        os.Id, peca.Sku.Valor);
                    continue;
                }
                houveMovimento |= Marcar(peca.Repor(os.Id));
            }
        }
        return houveMovimento;
    }

    private static bool TemReserva(Peca peca, Guid ordemServicoId) =>
        peca.Movimentacoes.Any(m => m.OrdemServicoId == ordemServicoId && m.Tipo == TipoMovimentacao.Reserva);

    /// <summary>
    /// Marca a movimentação como nova (Id Guid pré-setado alcançado pelo grafo viraria UPDATE no EF).
    /// </summary>
    private bool Marcar(MovimentacaoEstoque? movimentacao)
    {
        if (movimentacao is null) return false;
        _pecas.MarcarMovimentacaoComoNova(movimentacao);
        return true;
    }

    /// <summary>Uma <see cref="Peca"/> por <c>PecaId</c> (a OS pode ter a mesma peça em dois itens).</summary>
    private async Task<IReadOnlyList<Peca>> CarregarPecasAsync(OrdemDeServico os, CancellationToken ct)
    {
        var pecas = new List<Peca>();
        foreach (var pecaId in os.ItensPeca.Select(i => i.PecaId).Distinct())
        {
            pecas.Add(await _pecas.ObterPorIdAsync(pecaId, ct)
                ?? throw new InvalidOperationException($"Peça {pecaId} da OS {os.Id} não encontrada."));
        }
        return pecas;
    }

    private async Task<OrdemDeServico> ObterOsAsync(Guid id, CancellationToken ct) =>
        await _ordens.ObterPorIdAsync(id, ct)
            ?? throw new InvalidOperationException($"OS {id} não encontrada para o passo da saga.");
}
