# ADR-003 — Autenticação JWT e bootstrap do admin

**Status:** Aceita
**Data:** 2026-05-03

## Contexto

O desafio pede "JWT para APIs administrativas" e "consulta do cliente via API".

## Decisão

- JWT HS256, TTL de 60 minutos, sem refresh token (YAGNI no MVP)
- Senhas com BCrypt cost 12
- Rate limiting de 5 tentativas / 15 min por IP no `/auth/login`
- Cliente final consulta sua OS sem JWT, usando **número da OS + documento**
- Bootstrap automático: ao subir o container, se não existir admin, cria com
  `ADMIN_BOOTSTRAP_PASSWORD` e marca `PrecisaTrocarSenha = true`
- Políticas: `RequerAdmin` e `RequerAdminOuAtendente`

## Consequências

- ✅ Atende literalmente ao texto do desafio
- ✅ Cliente não precisa cadastro/login (reduz escopo MVP)
- ⚠️ Em produção: HS256 → RS256, segredos via Key Vault, refresh token
- ⚠️ Política anti-enumeração: `consulta` retorna 404 idêntico para "não existe"
  e "documento não confere" (não vaza existência de OS)
