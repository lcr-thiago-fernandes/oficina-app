# ADR-003 — Autenticação JWT e bootstrap do admin

**Status:** Superseded por [ADR-015](ADR-015-lambda-authorizer-hs256.md) (2026-09-15)
**Data:** 2026-05-03

> **Nota de 2026-09-15 (Fase 3).** O conteúdo abaixo está preservado como registro histórico e não
> descreve mais o sistema. O que mudou: o endpoint de login desta API foi **removido** — a API não
> emite mais token, só valida; o emissor único passou a ser a função `oficina-auth-api`
> ([ADR-015](ADR-015-lambda-authorizer-hs256.md)). A consulta do cliente deixou de ser anônima por
> "número da OS + documento" e passou a exigir token de perfil `Cliente`. O rate limiting de 5
> tentativas por IP virou contagem em DynamoDB, na Lambda. No login de cliente — o fluxo que
> substitui a consulta anônima — o balde é só por origem (IP); o balde por identidade (usuário)
> existe apenas em `/auth/admin`. Continuam válidos:
> HS256, TTL de 60 minutos, BCrypt custo 12 e a política anti-enumeração de responder 404 idêntico.

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
