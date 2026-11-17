# ADR-023 — Banco por serviço: PostgreSQL no OS Service, DynamoDB no Billing e na Execução

**Status:** Aceita
**Data:** 2026-11-17
**RFC relacionado:** [RFC-006](../rfc/RFC-006-microsservicos-saga-orquestrada.md)
**ADRs relacionados:** [ADR-002](ADR-002-postgresql.md), [ADR-006](ADR-006-saldo-denormalizado-estoque.md)

## Contexto

A Fase 4 exige que cada microsserviço tenha banco próprio, que nenhum serviço acesse o banco
de outro e que exista pelo menos um banco relacional e um não relacional.

## Decisão

| Serviço | Banco | Motivo |
|---|---|---|
| OS Service (`oficina-app`) | **PostgreSQL 16** (RDS do `oficina-infra-db`) | Domínio relacional (cliente ↔ veículo ↔ OS ↔ itens ↔ peças); abertura da OS e reserva de estoque numa transação `Serializable`; estado da saga e outbox na **mesma** transação do domínio |
| Billing (`oficina-billing-service`) | **Amazon DynamoDB** | Orçamento e pagamento são documentos por OS (itens embutidos), sempre lidos por chave (`pk = OS#<osId>`, `sk = ORCAMENTO \| PAGAMENTO#<id>`); escrita condicional (`attribute_not_exists(pk)`, `versao = :v`) dá idempotência e lock otimista; `WEBHOOK#<paymentId>` deduplica o webhook do Mercado Pago |
| Execução (`oficina-execucao-service`) | **Amazon DynamoDB** | Fila de execução lida por chave (`osId`) e por status/data (GSI `status-enfileiradaEm`); contador de OS ativas por unidade com escrita condicional transacional (`TransactWriteItems`) |

No PostgreSQL do OS Service, os schemas `clientes`, `catalogo`, `estoque`, `os` e `auth`
continuam os da Fase 3, e a Fase 4 acrescenta o schema `saga` (estado e trilha da saga,
`inbox_state`, `outbox_message`, `outbox_state` do MassTransit). Nenhuma migration da Fase 4
altera `auth` e `clientes`, lidos pela Lambda de autenticação (`oficina-lambda-auth`).

## Alternativas

- **MongoDB/DocumentDB** — modelo de documentos equivalente, mas o DocumentDB custa cerca de
  US$ 60/mês no menor porte e exige cluster e VPC; MongoDB próprio exigiria operação.
- **PostgreSQL para os três** — cumpriria o isolamento com schemas/instâncias separadas, mas não
  o requisito de um banco não relacional, e o acesso dos novos serviços é só por chave.

## Consequências

- ✅ DynamoDB on-demand custa centavos, já é usado na Fase 3 (rate limiting da Lambda) e tem
  LocalStack/DynamoDB Local para testes.
- ✅ O isolamento é físico: cada serviço só tem credencial (IRSA) para o próprio banco.
- ⚠️ O DynamoDB não participa de transação com o SQS: Billing e Execução não usam outbox
  ([ADR-024](ADR-024-outbox-e-idempotencia.md)).
- ⚠️ Relatórios que cruzam OS, pagamento e execução não podem ser feitos por *join*: vêm da
  telemetria (New Relic) ou de consultas às APIs.
