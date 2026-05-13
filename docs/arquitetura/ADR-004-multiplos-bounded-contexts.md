# ADR-004 — Múltiplos bounded contexts no monolito

**Status:** Aceita
**Data:** 2026-05-03

## Contexto

O Tech Challenge exige Event Storming e linguagem ubíqua. Tínhamos a opção de
modelar como um único contexto "Oficina" com agregados internos, ou dividir
em múltiplos contextos delimitados.

## Decisão

Quatro bounded contexts:

- **Gestão de Clientes** (suporte) — agregado `Cliente` com `Veiculo`
- **Catálogo de Serviços** (suporte) — agregado `Servico`
- **Estoque** (suporte) — agregado `Peca` com `MovimentacaoEstoque`
- **Ordem de Serviço** (núcleo) — agregado `OrdemDeServico` com `ItemServico` e `ItemPeca`

Comunicação síncrona em-processo via interfaces no Domínio. Todos compartilham
um único `OficinaDbContext` (transações cross-context).

## Consequências

- ✅ Event Storming muito mais rico
- ✅ Limites naturais (cada contexto tem invariantes próprias)
- ✅ Facilita migração futura para microsserviços
- ⚠️ Requer disciplina para não vazar entidades entre contextos
- ⚠️ Possível redundância (ex: snapshot de nome/preço duplica dado do catálogo)
  — mitigado por preservar histórico de OS independente de mudanças
