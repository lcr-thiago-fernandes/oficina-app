# ADR-024 — Transactional Outbox no OS Service e idempotência nos participantes

**Status:** Aceita
**Data:** 2026-11-17
**RFC relacionado:** [RFC-006](../rfc/RFC-006-microsservicos-saga-orquestrada.md)

## Contexto

Uma saga só compensa direito se nenhuma mensagem se perde e nenhuma é aplicada duas vezes. O
problema clássico é o *dual write*: gravar no banco e publicar no broker são duas operações;
se o processo cai entre elas, o banco diz uma coisa e a mensagem nunca sai (ou sai sem o
estado gravado). Além disso, o SQS entrega *at-least-once* e sem ordem garantida.

## Decisão

**OS Service — Transactional Outbox do MassTransit com EF Core**
(`DependencyInjectionMensageria.ConfigurarRegistro`):

- `AddEntityFrameworkOutbox<OficinaDbContext>` com `UsePostgres()` e `UseBusOutbox()`: o
  `Publish` do caso de uso de abertura (`OrdemServicoAberta`) e do cancelamento
  (`CancelamentoSolicitado`) só adiciona a mensagem ao `DbContext`; ela é gravada em
  `saga.outbox_message` no **mesmo** `SaveChanges` da OS, dos itens e da reserva de estoque. Um
  serviço em segundo plano entrega a mensagem depois do commit.
- `UseEntityFrameworkOutbox<OficinaDbContext>` em todo receive endpoint: o consumo da saga
  grava estado da saga, efeitos no domínio, comandos enviados e o registro de inbox
  (`saga.inbox_state`, deduplicação por `MessageId`) na mesma transação.
- Estado da saga e agregados com versão otimista (`xmin`); conflito → retry.

**Billing e Execução — sem outbox, com idempotência por escrita condicional**

- O DynamoDB não compartilha transação com o SQS, então não há outbox possível no mesmo
  sentido. Os dois serviços gravam no DynamoDB **antes** de publicar, e toda escrita é
  condicional (`attribute_not_exists`, `versao = :v`): a reentrega do mesmo comando não duplica
  o efeito. Quando o participante republica um evento já publicado (a Execução republica
  `ExecucaoEnfileirada` para um `IniciarExecucao` duplicado), a saga o ignora.

**Idempotência na saga** (`OrdemServicoSaga` + `PassosSagaOrdemServico`):

- Evento de etapa já passada → ignorado pela matriz `EventosIgnoradosPorEstado` (sem efeito).
- Evento de etapa futura → falha e volta por redelivery (nunca se perde, nunca adianta passo).
- Os passos locais não regridem status (`JaAlcancou`) e o estoque não é liberado/reposto duas
  vezes (o domínio devolve `null` quando a reserva já foi desfeita).
- Pagamento confirmado depois do cancelamento → `EstornarPagamento`, não perda do dinheiro.

## Alternativas

- **Publicar direto depois do `SaveChanges`** — simples, mas perde a mensagem se o processo cair
  entre o commit e o publish.
- **Change Data Capture / DynamoDB Streams nos participantes** — resolveria o *dual write* no
  Billing/Execução, mas acrescenta infraestrutura (Lambda/Kinesis) desproporcional ao projeto.

## Consequências

- ✅ Nenhuma mensagem da saga se perde no OS Service; duplicatas não geram efeito duplicado.
- ✅ Cenários cobertos por teste: `OrdemServicoSagaTestes` (`EventoDuplicado_NaoReexecutaPasso`,
  `StatusExecucaoAlterado_AntesDaEnfileirada_FalhaParaRedeliverySemEfeito`,
  `ExecucaoEnfileiradaRepublicada_EmEstadosPosteriores_EIgnoradaSemErro`,
  `PagamentoConfirmado_AposCancelamento_EnviaEstornarPagamento`) e `InicioDaSagaTestes`
  (`AbrirOrdem_SaldoInsuficiente_NaoGravaMensagemNoOutbox`,
  `PagamentoConfirmado_FalhaAoGravarOutbox_DesfazEstoqueESagaNaMesmaTransacao`,
  `PagamentoConfirmado_ConflitoDeXminNaPeca_RetentaComContextoNovoEBaixaUmaVez`).
- ⚠️ Nos participantes existe uma janela (gravou no DynamoDB, caiu antes de publicar) em que o
  evento pode não sair: o Billing não republica `OrcamentoGerado` para um `GerarOrcamento`
  reentregue. A saga tolera isso — a decisão do cliente (`OrcamentoAprovado`/`OrcamentoRejeitado`)
  chegando sem o `OrcamentoGerado` registra o orçamento como gerado implicitamente.
