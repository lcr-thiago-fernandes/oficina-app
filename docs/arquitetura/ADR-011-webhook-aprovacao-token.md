# ADR-011 — Aprovação de orçamento via webhook autenticado por token

**Status:** Aceita
**Data:** 2026-07-06

## Contexto

Na Fase 1 o cliente aprovava/rejeitava o orçamento por rotas públicas
`POST /consulta/{numero}/aprovar|rejeitar` (sem autenticação, protegidas só pelo
documento). Para a Fase 2 quisemos um mecanismo adequado a integração externa
(portal do cliente, gateway de mensagens) e mais seguro que uma rota pública que
altera estado sem credencial.

## Decisão

Substituir os POSTs públicos por um **webhook autenticado por token**:

- `POST /api/v1/ordens-servico/{id}/orcamento/aprovacao`
- Header obrigatório **`X-Webhook-Token`**, validado por `ValidacaoTokenWebhookFilter`
  (um `IAuthorizationFilter` que roda **antes** do model binding — assim 401 tem
  precedência sobre 400/415 quando o corpo falta ou é inválido).
- Comparação do token em **tempo constante** (`CryptographicOperations.FixedTimeEquals`)
  e **fail-closed**: sem token configurado, nenhuma requisição é autorizada.
- Corpo: `{ "decisao": "aprovado" | "recusado" }` (qualquer outro valor → 422).
  `aprovado` chama `OrdemDeServico.Aprovar()`; `recusado` chama `Rejeitar()` (cancela).
- O token vem de configuração (`Webhook:Token`) — `.env` local (`WEBHOOK_TOKEN`) e
  Kubernetes Secret (`Webhook__Token`) em produção. Nunca hardcoded.

O `ConsultaController` permanece **apenas com GET** (consulta anti-enumeração), sem
mais mutação de estado.

## Consequências

- ✅ Mutação de estado exige credencial (token), não só o documento
- ✅ Proteção contra timing attack e contra operar sem token (fail-closed)
- ✅ 401 correto mesmo sem corpo (filtro antes do binding)
- ✅ Consulta pública fica read-only (menor superfície de ataque)
- ⚠️ Token único compartilhado (MVP); rotação/HMAC por payload ficam para evolução futura
