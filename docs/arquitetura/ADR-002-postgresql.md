# ADR-002 — PostgreSQL como banco de dados

**Status:** Aceita
**Data:** 2026-05-03

## Contexto

Domínio altamente relacional (cliente↔veículos↔OS↔itens↔peças↔movimentações),
com necessidade de transações fortes (aprovar orçamento + baixar estoque tem
que ser atômico). O desafio dá liberdade na escolha mas exige justificativa.

Avaliamos PostgreSQL, SQL Server, MySQL, MongoDB.

## Decisão

**PostgreSQL 16**.

## Justificativa

- ACID forte por padrão (essencial para integridade de orçamento + estoque)
- Joins ricos (consultas frequentes entre cliente/veículo/OS)
- Suporte a `BIGSERIAL` (número humano-amigável da OS)
- Schemas separados (`clientes.*`, `os.*`, `estoque.*`, `catalogo.*`, `auth.*`)
  reforçam limites dos bounded contexts mesmo no monolito
- Transação serializável testada e usada em saída de estoque
- Gratuito, maduro, ecossistema .NET excelente (Npgsql)
- MongoDB exigiria modelar agregados com referências por documento — possível,
  mas custoso de justificar em um domínio tão relacional

## Consequências

- ✅ Migrations via EF Core
- ✅ Roda em container leve (`postgres:16-alpine`)
- ⚠️ Em produção, requer estratégia de backup/restauração e monitoramento
