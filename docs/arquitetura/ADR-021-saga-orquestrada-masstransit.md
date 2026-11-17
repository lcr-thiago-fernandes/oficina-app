# ADR-021 — Saga orquestrada com MassTransit State Machine no OS Service

**Status:** Aceita
**Data:** 2026-11-17
**RFC relacionado:** [RFC-006](../rfc/RFC-006-microsservicos-saga-orquestrada.md)

## Contexto

Na Fase 4 o ciclo da OS atravessa três serviços: o OS Service (`oficina-app`) abre a OS e
reserva o estoque, o Billing (`oficina-billing-service`) gera o orçamento, recebe a decisão do
cliente e cobra pelo Mercado Pago, e a Execução (`oficina-execucao-service`) enfileira,
diagnostica, repara e finaliza. Cada serviço tem banco próprio, então não existe transação
ACID entre eles: uma falha no meio (orçamento inválido, rejeição, pagamento recusado, fila de
execução lotada) precisa ser desfeita por **transações compensatórias**.

A saga tem quatro participantes lógicos (OS, Billing, Mercado Pago via Billing, Execução),
três pontos de falha depois da abertura e uma compensação em cascata (execução recusada →
estornar pagamento → repor estoque → cancelar a OS).

## Decisão

Saga **orquestrada**, com o orquestrador **dentro do OS Service**, implementada como
`MassTransitStateMachine<EstadoSagaOrdemServico>`
(`src/Oficina.Infraestrutura/Mensageria/Saga/OrdemServicoSaga.cs`):

- Estados: `AguardandoOrcamento`, `AguardandoAprovacao`, `AguardandoPagamento`,
  `AguardandoExecucao`, `EmExecucao`, `Compensando`, `Concluida`, `Cancelada`.
- Correlação por `CorrelationId = OrdemServicoId`. A saga nasce só do evento interno
  `OrdemServicoAberta` (publicado pelo caso de uso de abertura via outbox); qualquer outro
  evento sem saga existente falha e volta por retry/redelivery.
- Comandos (`GerarOrcamento`, `EstornarPagamento`, `CancelarOrcamento`, `IniciarExecucao`) são
  *send* para a fila do participante; os participantes respondem com eventos (*publish*). Os
  participantes não conhecem a saga.
- Os efeitos locais de cada passo (status da OS, reserva/baixa/liberação/reposição de estoque,
  histórico, notificação) ficam na porta `IPassosSagaOrdemServico`
  (`src/Oficina.Aplicacao/OrdensServico/Saga/PassosSagaOrdemServico.cs`), testável sem bus.
- Estado e trilha persistidos no schema PostgreSQL `saga` (`ordem_servico_saga`,
  `ordem_servico_saga_passo`), com concorrência otimista por `xmin`. A saga não é finalizada
  (`Finalize()`): a linha fica para o `GET /api/v1/ordens-servico/{id}/saga`.
- Evento de etapa **já passada** (duplicado/atrasado) é ignorado por uma matriz explícita
  (`EventosIgnoradosPorEstado`); evento de etapa **futura** não é declarado no estado atual,
  falha e volta por redelivery até a saga chegar lá.

## Alternativas

- **Coreografia** — sem ponto central, mas o fluxo e as compensações ficariam espalhados em
  três repositórios, com dependência cíclica (OS → Billing → OS → Execução → Billing para o
  estorno) e sem um lugar único para testar o fluxo ponta a ponta.
- **AWS Step Functions** — gerenciado e visual, mas a lógica sai do código .NET, não roda no
  test harness nem no BDD do CI, e acopla o fluxo à AWS.

## Consequências

- ✅ Fluxo e compensações legíveis em um arquivo; dependências em mão única.
- ✅ Testável sem infraestrutura: `OrdemServicoSagaTestes` (test harness do MassTransit) e o
  BDD `FluxoCompletoOrdemServico.feature` rodam a saga real no CI.
- ⚠️ O orquestrador é peça crítica: mitigado por estado persistido no PostgreSQL, outbox
  transacional ([ADR-024](ADR-024-outbox-e-idempotencia.md)) e réplicas no Kubernetes (HPA).
- ⚠️ `EstornoFalhou` deixa a saga em `Compensando` (intervenção manual; alerta Critical no
  New Relic). Não há timeout de aprovação (não-objetivo da Fase 4).
