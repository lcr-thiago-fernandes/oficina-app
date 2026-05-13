# ADR-008 — Soft delete em entidades referenciadas

**Status:** Aceita
**Data:** 2026-05-03

## Contexto

Cliente, Veículo, Serviço e Peça podem ser referenciados por OSs antigas. Hard
delete quebraria histórico e relatórios.

## Decisão

`Cliente`, `Servico` e `Peca` têm coluna `Ativo` (bool). DELETE no controller
chama `Inativar()` no agregado, que marca `Ativo = false`. Listagens padrão
filtram `Ativo = true`; listagens administrativas podem incluir inativos via
flag `incluirInativos`.

## Consequências

- ✅ Histórico preservado
- ✅ Restauração trivial (`Ativar()`)
- ⚠️ Documento/SKU de inativo continua único (não pode reusar)
- ⚠️ LGPD: "esqueçer" cliente requer estratégia diferente (anonimização) —
  fora do escopo do MVP
