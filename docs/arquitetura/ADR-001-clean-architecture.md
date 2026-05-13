# ADR-001 — Clean Architecture com 4 projetos

**Status:** Aceita
**Data:** 2026-05-03

## Contexto

O Tech Challenge exige "monolito com arquitetura em camadas" e DDD aplicado.
Era preciso decidir entre camadas clássicas (Presentation/Application/Domain/Infrastructure
sem inversão), Clean Architecture (com inversão de dependência rigorosa) ou
Modular Monolith por bounded context.

## Decisão

Adotamos **Clean Architecture** com 4 projetos: `Oficina.Api`, `Oficina.Aplicacao`,
`Oficina.Dominio`, `Oficina.Infraestrutura`. Regras de dependência:

- `Dominio` → nada
- `Aplicacao` → `Dominio`
- `Infraestrutura` → `Dominio` + `Aplicacao` (implementa interfaces)
- `Api` → `Aplicacao` + `Infraestrutura` (apenas para wiring de DI)

## Consequências

- ✅ Facilita testes (mock de repositórios via interfaces no Domínio)
- ✅ Domínio puro, sem dependência de framework
- ✅ Fica próximo do "camadas" pedido pelo desafio mas com mais maturidade
- ⚠️ Exige mais boilerplate (interfaces) que camadas planas
- ⚠️ Modular Monolith seria mais robusto para crescer mas overkill para um MVP de fase 1
