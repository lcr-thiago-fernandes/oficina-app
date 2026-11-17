using System.Linq.Expressions;
using MassTransit;
using Microsoft.Extensions.Options;
using Oficina.Aplicacao.OrdensServico.Saga;
using Oficina.Aplicacao.OrdensServico.Saga.Dtos;
using Oficina.Infraestrutura.Mensageria.Contratos;
using Oficina.Infraestrutura.Mensageria.Internos;

namespace Oficina.Infraestrutura.Mensageria.Saga;

/// <summary>
/// Saga orquestrada da OS (spec 4.2). Efeitos locais via <see cref="IPassosSagaOrdemServico"/>,
/// comandos para as filas do Billing e da Execução, trilha via <see cref="IRegistradorPassoSaga"/>.
/// <list type="bullet">
/// <item>Evento de etapa <b>já passada</b> no estado atual (duplicado, atrasado, republicado) →
/// ignorado sem efeito (<see cref="EventosIgnoradosPorEstado"/>).</item>
/// <item>Evento de etapa <b>futura</b> → não declarado: falha (<c>UnhandledEventException</c>) e volta
/// por retry/redelivery até a saga chegar lá. Nenhum passo é chamado antes da hora.</item>
/// </list>
/// Não usa <c>Finalize()</c>: a linha da saga fica para o <c>GET /ordens-servico/{id}/saga</c>.
/// </summary>
public sealed class OrdemServicoSaga : MassTransitStateMachine<EstadoSagaOrdemServico>
{
    internal const string MotivoPagamentoAposCancelamento = "Pagamento confirmado após cancelamento da OS";

    internal const string MotivoPagamentoNaoHonrado =
        "Pagamento confirmado com a OS cancelada ou a reserva de estoque liberada";

    private const string DetalheSemOrcamentoGerado = "OrcamentoGerado não recebido: inferido da decisão do cliente";

    private static readonly string[] EventosAteOPagamento =
    {
        nameof(OrdemServicoAberta), nameof(OrcamentoGerado), nameof(OrcamentoFalhou),
        nameof(OrcamentoAprovado), nameof(OrcamentoRejeitado),
        nameof(PagamentoConfirmado), nameof(PagamentoRecusado),
    };

    /// <summary>
    /// Matriz de Ignore: por estado, os eventos de etapas já passadas (sem efeito e sem erro).
    /// Exposta ao teste, que percorre exatamente esta tabela.
    /// </summary>
    internal static readonly IReadOnlyDictionary<string, string[]> EventosIgnoradosPorEstado =
        new Dictionary<string, string[]>
        {
            [nameof(AguardandoOrcamento)] = new[] { nameof(OrdemServicoAberta) },
            [nameof(AguardandoAprovacao)] = new[] { nameof(OrdemServicoAberta), nameof(OrcamentoGerado), nameof(OrcamentoFalhou) },
            [nameof(AguardandoPagamento)] = new[]
            {
                nameof(OrdemServicoAberta), nameof(OrcamentoGerado), nameof(OrcamentoFalhou),
                nameof(OrcamentoAprovado), nameof(OrcamentoRejeitado),
            },
            [nameof(AguardandoExecucao)] = EventosAteOPagamento,
            [nameof(EmExecucao)] = EventosAteOPagamento
                .Concat(new[] { nameof(ExecucaoEnfileirada), nameof(ExecucaoRecusada) }).ToArray(),
            // PagamentoConfirmado é tratado: estorna um pagamento diferente do que já está sendo estornado.
            [nameof(Compensando)] = EventosAteOPagamento.Where(e => e != nameof(PagamentoConfirmado))
                .Concat(new[]
                {
                    nameof(ExecucaoEnfileirada), nameof(ExecucaoRecusada),
                    nameof(StatusExecucaoAlterado), nameof(ExecucaoFinalizada),
                }).ToArray(),
            [nameof(Concluida)] = TodosOsEventos(),
            // PagamentoConfirmado é tratado: pagamento depois do cancelamento é estornado.
            [nameof(Cancelada)] = TodosOsEventos().Where(e => e != nameof(PagamentoConfirmado)).ToArray(),
        };

    private readonly NomesMensageria _nomes;

    public State AguardandoOrcamento { get; private set; } = null!;
    public State AguardandoAprovacao { get; private set; } = null!;
    public State AguardandoPagamento { get; private set; } = null!;
    public State AguardandoExecucao { get; private set; } = null!;
    public State EmExecucao { get; private set; } = null!;
    public State Compensando { get; private set; } = null!;
    public State Concluida { get; private set; } = null!;
    public State Cancelada { get; private set; } = null!;

    public Event<OrdemServicoAberta> OrdemServicoAberta { get; private set; } = null!;
    public Event<CancelamentoSolicitado> CancelamentoSolicitado { get; private set; } = null!;
    public Event<OrcamentoGerado> OrcamentoGerado { get; private set; } = null!;
    public Event<OrcamentoFalhou> OrcamentoFalhou { get; private set; } = null!;
    public Event<OrcamentoAprovado> OrcamentoAprovado { get; private set; } = null!;
    public Event<OrcamentoRejeitado> OrcamentoRejeitado { get; private set; } = null!;
    public Event<PagamentoConfirmado> PagamentoConfirmado { get; private set; } = null!;
    public Event<PagamentoRecusado> PagamentoRecusado { get; private set; } = null!;
    public Event<ExecucaoEnfileirada> ExecucaoEnfileirada { get; private set; } = null!;
    public Event<ExecucaoRecusada> ExecucaoRecusada { get; private set; } = null!;
    public Event<StatusExecucaoAlterado> StatusExecucaoAlterado { get; private set; } = null!;
    public Event<ExecucaoFinalizada> ExecucaoFinalizada { get; private set; } = null!;
    public Event<PagamentoEstornado> PagamentoEstornado { get; private set; } = null!;
    public Event<EstornoFalhou> EstornoFalhou { get; private set; } = null!;

    public OrdemServicoSaga(IOptions<OpcoesMensageria> opcoes)
    {
        _nomes = new NomesMensageria(opcoes.Value.PrefixoAmbiente);

        InstanceState(x => x.EstadoAtual);
        DeclararEventos();

        Initially(
            When(OrdemServicoAberta)
                .ThenAsync(async ctx =>
                {
                    var pedido = await Passos(ctx).MontarPedidoOrcamentoAsync(ctx.Saga.CorrelationId, ctx.CancellationToken);
                    ctx.Saga.NumeroOs = pedido.NumeroOs;
                    ctx.Saga.CriadaEm = ctx.Saga.AtualizadaEm = DateTime.UtcNow;
                    Guardar(ctx, pedido);
                })
                .Send(_nomes.EnderecoComandosBilling, ctx => CriarGerarOrcamento(ctx.Saga, Obter<PedidoOrcamento>(ctx)!))
                .TransitionTo(AguardandoOrcamento)
                .Then(ctx => Registrar(ctx, PassosSaga.SolicitarOrcamento, PassosSaga.Sucesso, false)));

        During(AguardandoOrcamento,
            When(OrcamentoGerado)
                .ThenAsync(ctx => Passos(ctx).RegistrarOrcamentoGeradoAsync(ctx.Saga.CorrelationId, ctx.CancellationToken))
                .TransitionTo(AguardandoAprovacao)
                .Then(ctx => Registrar(ctx, PassosSaga.GerarOrcamento, PassosSaga.Sucesso, false)),
            When(OrcamentoFalhou)
                .Then(ctx => ctx.Saga.Motivo = ctx.Message.Motivo)
                .ThenAsync(ctx => Passos(ctx).CancelarAsync(ctx.Saga.CorrelationId, ctx.Saga.Motivo!, ctx.CancellationToken))
                .TransitionTo(Cancelada)
                .Then(ctx => Registrar(ctx, PassosSaga.GerarOrcamento, PassosSaga.Falha, true)),
            // Billing pode cair entre salvar o orçamento e publicar OrcamentoGerado (o GerarOrcamento
            // reentregue não republica): a decisão do cliente chega sem o OrcamentoGerado. A OS
            // passa por AguardandoAprovacao antes, como no caminho normal.
            AprovarOrcamento(RegistrarOrcamentoGeradoImplicito(When(OrcamentoAprovado))),
            RejeitarOrcamento(RegistrarOrcamentoGeradoImplicito(When(OrcamentoRejeitado))));

        During(AguardandoAprovacao,
            AprovarOrcamento(When(OrcamentoAprovado)),
            RejeitarOrcamento(When(OrcamentoRejeitado)));

        During(AguardandoPagamento,
            When(PagamentoConfirmado)
                .ThenAsync(async ctx =>
                {
                    ctx.Saga.PagamentoId = ctx.Message.PagamentoId;
                    Guardar(ctx, await Passos(ctx).ConfirmarPagamentoAsync(ctx.Saga.CorrelationId, ctx.CancellationToken));
                })
                .IfElse(ctx => Obter<PedidoExecucao>(ctx) is not null,
                    confirmado => confirmado
                        .Send(_nomes.EnderecoComandosExecucao, ctx => CriarIniciarExecucao(ctx.Saga, Obter<PedidoExecucao>(ctx)!))
                        .TransitionTo(AguardandoExecucao)
                        .Then(ctx => Registrar(ctx, PassosSaga.ConfirmarPagamento, PassosSaga.Sucesso, false)),
                    // OS cancelada ou reserva liberada antes do pagamento: nada foi baixado do estoque;
                    // o pagamento é estornado e a compensação termina em Cancelada (PagamentoEstornado).
                    naoHonrado => naoHonrado
                        .Then(ctx => ctx.Saga.Motivo = MotivoPagamentoNaoHonrado)
                        .Send(_nomes.EnderecoComandosBilling, ctx => CriarEstornarPagamento(ctx.Saga, MotivoPagamentoNaoHonrado))
                        .TransitionTo(Compensando)
                        .Then(ctx => Registrar(ctx, PassosSaga.ConfirmarPagamento, PassosSaga.Falha, true, ctx.Message.PagamentoId))),
            // Qualquer motivo do Billing (cartao_recusado, valor_divergente, pagamento_estornado...) é
            // falha de pagamento. O Billing já cancelou o orçamento: sem EstornarPagamento nem CancelarOrcamento.
            When(PagamentoRecusado)
                .Then(ctx => ctx.Saga.Motivo = ctx.Message.Motivo)
                .ThenAsync(ctx => Passos(ctx).CancelarAsync(ctx.Saga.CorrelationId, ctx.Saga.Motivo!, ctx.CancellationToken))
                .TransitionTo(Cancelada)
                .Then(ctx => Registrar(ctx, PassosSaga.ConfirmarPagamento, PassosSaga.Falha, true, ctx.Message.Motivo)));

        During(AguardandoOrcamento, AguardandoAprovacao, AguardandoPagamento,
            When(CancelamentoSolicitado)
                .Then(ctx => ctx.Saga.Motivo = ctx.Message.Motivo)
                .ThenAsync(ctx => Passos(ctx).CancelarAsync(ctx.Saga.CorrelationId, ctx.Saga.Motivo!, ctx.CancellationToken))
                .Send(_nomes.EnderecoComandosBilling, ctx => new CancelarOrcamento
                {
                    OrdemServicoId = ctx.Saga.CorrelationId,
                    OcorridoEm = DateTime.UtcNow,
                    Motivo = ctx.Saga.Motivo!,
                })
                .TransitionTo(Cancelada)
                .Then(ctx => Registrar(ctx, PassosSaga.Cancelamento, PassosSaga.Compensado, true)));

        // Pagamento já confirmado: o atendente não cancela mais (o CancelarOrdemUseCase barra; isto
        // cobre a janela entre a checagem dele e o consumo). Fica registrado na trilha.
        During(AguardandoExecucao, EmExecucao, Compensando,
            When(CancelamentoSolicitado)
                .Then(ctx => Registrar(ctx, PassosSaga.Cancelamento, PassosSaga.Ignorado, false, ctx.Message.Motivo)));

        During(AguardandoExecucao,
            When(ExecucaoEnfileirada)
                .ThenAsync(ctx => Passos(ctx).RegistrarExecucaoEnfileiradaAsync(ctx.Saga.CorrelationId, ctx.CancellationToken))
                .TransitionTo(EmExecucao)
                .Then(ctx => Registrar(ctx, PassosSaga.EnfileirarExecucao, PassosSaga.Sucesso, false)),
            When(ExecucaoRecusada)
                .Then(ctx => ctx.Saga.Motivo = ctx.Message.Motivo)
                .Send(_nomes.EnderecoComandosBilling, ctx => CriarEstornarPagamento(ctx.Saga, ctx.Message.Motivo))
                .TransitionTo(Compensando)
                .Then(ctx => Registrar(ctx, PassosSaga.EnfileirarExecucao, PassosSaga.Falha, true)));

        During(EmExecucao,
            // Status repetido (ou atrasado) não muda a OS e não vira passo na trilha.
            When(StatusExecucaoAlterado)
                .ThenAsync(async ctx => Guardar(ctx, new StatusExecucaoAplicado(await Passos(ctx).RegistrarStatusExecucaoAsync(
                    ctx.Saga.CorrelationId, MapearEtapa(ctx.Message.StatusExecucao), ctx.CancellationToken))))
                .If(ctx => Obter<StatusExecucaoAplicado>(ctx)!.Aplicado,
                    aplicado => aplicado
                        .Then(ctx => Registrar(ctx, PassosSaga.Execucao, PassosSaga.Sucesso, false, ctx.Message.StatusExecucao.ToString()))),
            When(ExecucaoFinalizada)
                .ThenAsync(ctx => Passos(ctx).RegistrarExecucaoFinalizadaAsync(ctx.Saga.CorrelationId, ctx.CancellationToken))
                .TransitionTo(Concluida)
                .Then(ctx => Registrar(ctx, PassosSaga.FinalizarExecucao, PassosSaga.Sucesso, false)));

        During(Compensando,
            When(PagamentoEstornado)
                .ThenAsync(ctx => Passos(ctx).CancelarAsync(
                    ctx.Saga.CorrelationId, ctx.Saga.Motivo ?? "Pagamento estornado", ctx.CancellationToken))
                .TransitionTo(Cancelada)
                .Then(ctx => Registrar(ctx, PassosSaga.EstornarPagamento, PassosSaga.Compensado, true)),
            // Nada no domínio: intervenção manual. A saga fica em Compensando com o motivo.
            When(EstornoFalhou)
                .Then(ctx => ctx.Saga.Motivo = "Estorno falhou: " + ctx.Message.Motivo)
                .Then(ctx => Registrar(ctx, PassosSaga.EstornarPagamento, PassosSaga.CompensacaoFalhou, true, ctx.Message.Motivo)),
            // O mesmo pagamento (já em estorno) é duplicata; outro pagamento da mesma OS também é estornado.
            // Nunca baixa estoque (ConfirmarPagamentoAsync) numa OS em compensação.
            When(PagamentoConfirmado)
                .If(ctx => ctx.Message.PagamentoId != ctx.Saga.PagamentoId,
                    outro => outro
                        .Send(_nomes.EnderecoComandosBilling, ctx => CriarEstornarPagamento(ctx.Saga, MotivoPagamentoAposCancelamento))
                        .Then(ctx => Registrar(ctx, PassosSaga.EstornarPagamento, PassosSaga.Falha, true, ctx.Message.PagamentoId))));

        // Pagamento chegou depois do cancelamento (corrida com CancelamentoSolicitado, Review Focus 2):
        // estorna em vez de perder o dinheiro. PagamentoId igual ao já registrado = pagamento que já foi
        // estornado (a saga só chega a Cancelada com PagamentoId depois de PagamentoEstornado) → duplicata.
        During(Cancelada,
            When(PagamentoConfirmado)
                .If(ctx => ctx.Message.PagamentoId != ctx.Saga.PagamentoId,
                    tardio => tardio
                        .Then(ctx => ctx.Saga.PagamentoId = ctx.Message.PagamentoId)
                        .Send(_nomes.EnderecoComandosBilling, ctx => CriarEstornarPagamento(ctx.Saga, MotivoPagamentoAposCancelamento))
                        .TransitionTo(Compensando)
                        .Then(ctx => Registrar(ctx, PassosSaga.EstornarPagamento, PassosSaga.Falha, true, ctx.Message.PagamentoId))));

        foreach (var (estado, eventos) in EventosIgnoradosPorEstado)
            During(GetState(estado), eventos.Select(e => Ignore(((StateMachine)this).GetEvent(e))).ToArray());
    }

    private void DeclararEventos()
    {
        // Sem OnMissingInstance: é o único evento que cria a saga.
        Event(() => OrdemServicoAberta, e =>
        {
            e.CorrelateById(c => c.Message.OrdemServicoId);
            e.InsertOnInitial = true;
            e.SetSagaFactory(c => new EstadoSagaOrdemServico { CorrelationId = c.Message.OrdemServicoId });
        });

        Correlacionar(() => CancelamentoSolicitado);
        Correlacionar(() => OrcamentoGerado);
        Correlacionar(() => OrcamentoFalhou);
        Correlacionar(() => OrcamentoAprovado);
        Correlacionar(() => OrcamentoRejeitado);
        Correlacionar(() => PagamentoConfirmado);
        Correlacionar(() => PagamentoRecusado);
        Correlacionar(() => ExecucaoEnfileirada);
        Correlacionar(() => ExecucaoRecusada);
        Correlacionar(() => StatusExecucaoAlterado);
        Correlacionar(() => ExecucaoFinalizada);
        Correlacionar(() => PagamentoEstornado);
        Correlacionar(() => EstornoFalhou);
    }

    /// <summary>Saga inexistente → falha (retry/redelivery): o evento pode ter chegado antes da abertura.</summary>
    private void Correlacionar<T>(Expression<Func<Event<T>>> evento) where T : class, IMensagemDaSaga =>
        Event(evento, e =>
        {
            e.CorrelateById(c => c.Message.OrdemServicoId);
            e.OnMissingInstance(m => m.Fault());
        });

    private static string[] TodosOsEventos() => new[]
    {
        nameof(OrdemServicoAberta), nameof(OrcamentoGerado), nameof(OrcamentoFalhou),
        nameof(OrcamentoAprovado), nameof(OrcamentoRejeitado),
        nameof(PagamentoConfirmado), nameof(PagamentoRecusado),
        nameof(ExecucaoEnfileirada), nameof(ExecucaoRecusada),
        nameof(StatusExecucaoAlterado), nameof(ExecucaoFinalizada),
        nameof(PagamentoEstornado), nameof(EstornoFalhou), nameof(CancelamentoSolicitado),
    };

    private EventActivityBinder<EstadoSagaOrdemServico, T> RegistrarOrcamentoGeradoImplicito<T>(
        EventActivityBinder<EstadoSagaOrdemServico, T> binder) where T : class =>
        binder
            .ThenAsync(ctx => Passos(ctx).RegistrarOrcamentoGeradoAsync(ctx.Saga.CorrelationId, ctx.CancellationToken))
            .TransitionTo(AguardandoAprovacao)
            .Then(ctx => Registrar(ctx, PassosSaga.GerarOrcamento, PassosSaga.Sucesso, false, DetalheSemOrcamentoGerado));

    private EventActivityBinder<EstadoSagaOrdemServico, OrcamentoAprovado> AprovarOrcamento(
        EventActivityBinder<EstadoSagaOrdemServico, OrcamentoAprovado> binder) =>
        binder
            .ThenAsync(ctx => Passos(ctx).RegistrarOrcamentoAprovadoAsync(ctx.Saga.CorrelationId, ctx.CancellationToken))
            .TransitionTo(AguardandoPagamento)
            .Then(ctx => Registrar(ctx, PassosSaga.AprovarOrcamento, PassosSaga.Sucesso, false));

    private EventActivityBinder<EstadoSagaOrdemServico, OrcamentoRejeitado> RejeitarOrcamento(
        EventActivityBinder<EstadoSagaOrdemServico, OrcamentoRejeitado> binder) =>
        binder
            .Then(ctx => ctx.Saga.Motivo = "Orçamento rejeitado: " + (ctx.Message.Motivo ?? "sem motivo"))
            .ThenAsync(ctx => Passos(ctx).RegistrarOrcamentoRejeitadoAsync(
                ctx.Saga.CorrelationId, ctx.Message.Motivo, ctx.CancellationToken))
            .TransitionTo(Cancelada)
            .Then(ctx => Registrar(ctx, PassosSaga.AprovarOrcamento, PassosSaga.Falha, true));

    private static IPassosSagaOrdemServico Passos(ConsumeContext ctx) =>
        ctx.GetServiceOrCreateInstance<IPassosSagaOrdemServico>();

    /// <summary>
    /// Grava o passo na trilha. Sempre chamado <b>depois</b> do <c>TransitionTo</c> do mesmo evento: o
    /// registrador lê <see cref="EstadoSagaOrdemServico.EstadoAtual"/>, que deve ser o estado novo.
    /// </summary>
    private static void Registrar(
        BehaviorContext<EstadoSagaOrdemServico> ctx, string passo, string resultado, bool compensacao, string? detalhe = null) =>
        ctx.GetServiceOrCreateInstance<IRegistradorPassoSaga>().Registrar(ctx.Saga, passo, resultado, compensacao, detalhe);

    /// <summary>
    /// Resultado de um passo guardado no contexto do consumo para as atividades seguintes do mesmo
    /// evento (o envio do comando e a escolha do próximo estado). Não é persistido.
    /// </summary>
    private sealed class ResultadoDoPasso<T> where T : class
    {
        public ResultadoDoPasso(T? valor) => Valor = valor;
        public T? Valor { get; }
    }

    private sealed record StatusExecucaoAplicado(bool Aplicado);

    private static void Guardar<T>(PipeContext ctx, T? valor) where T : class =>
        ctx.AddOrUpdatePayload(() => new ResultadoDoPasso<T>(valor), _ => new ResultadoDoPasso<T>(valor));

    private static T? Obter<T>(PipeContext ctx) where T : class =>
        ctx.TryGetPayload<ResultadoDoPasso<T>>(out var resultado)
            ? resultado.Valor
            : throw new InvalidOperationException($"Resultado do passo ({typeof(T).Name}) ausente no contexto da saga.");

    private static GerarOrcamento CriarGerarOrcamento(EstadoSagaOrdemServico saga, PedidoOrcamento pedido) => new()
    {
        OrdemServicoId = saga.CorrelationId,
        OcorridoEm = DateTime.UtcNow,
        NumeroOs = pedido.NumeroOs,
        ClienteDocumento = pedido.ClienteDocumento,
        ClienteNome = pedido.ClienteNome,
        ClienteEmail = pedido.ClienteEmail,
        Itens = pedido.Itens.Select(i => new ItemOrcamento
        {
            Tipo = Enum.Parse<TipoItemOrcamento>(i.Tipo.ToString()),
            ReferenciaId = i.ReferenciaId,
            Descricao = i.Descricao,
            Quantidade = i.Quantidade,
            PrecoUnitario = i.PrecoUnitario,
        }).ToList(),
    };

    private static IniciarExecucao CriarIniciarExecucao(EstadoSagaOrdemServico saga, PedidoExecucao pedido) => new()
    {
        OrdemServicoId = saga.CorrelationId,
        OcorridoEm = DateTime.UtcNow,
        NumeroOs = pedido.NumeroOs,
        Unidade = pedido.Unidade,
        Servicos = pedido.Servicos.Select(i => new ItemExecucao { Descricao = i.Descricao, Quantidade = i.Quantidade }).ToList(),
        Pecas = pedido.Pecas.Select(i => new ItemExecucao { Descricao = i.Descricao, Quantidade = i.Quantidade }).ToList(),
    };

    private static EstornarPagamento CriarEstornarPagamento(EstadoSagaOrdemServico saga, string motivo) => new()
    {
        OrdemServicoId = saga.CorrelationId,
        OcorridoEm = DateTime.UtcNow,
        Motivo = motivo,
    };

    private static EtapaExecucao MapearEtapa(StatusExecucao status) => Enum.Parse<EtapaExecucao>(status.ToString());
}
