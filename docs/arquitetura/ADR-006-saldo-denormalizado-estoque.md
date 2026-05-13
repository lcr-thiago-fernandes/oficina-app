# ADR-006 — Saldo denormalizado em `Peca.SaldoAtual`

**Status:** Aceita
**Data:** 2026-05-03

## Contexto

Saldo de uma peça pode ser calculado como `SOMA(entradas) - SOMA(saidas)` sobre
`MovimentacaoEstoque`. Mas leitura é frequente (cada listagem mostra saldo) e
o cálculo cresce linearmente com o histórico.

## Decisão

`Peca` mantém `SaldoAtual` como coluna denormalizada. É atualizado no agregado
em `RegistrarEntrada` e `RegistrarSaida` e persistido na **mesma transação**
que insere a `MovimentacaoEstoque`.

Saídas concorrentes usam `IsolationLevel.Serializable` no caso de uso para
evitar duas saídas paralelas zerarem o saldo simultaneamente.

## Consequências

- ✅ Leitura de saldo é O(1)
- ✅ Consistência garantida pela transação
- ⚠️ Cuidado para não atualizar `SaldoAtual` fora dos métodos do agregado
- ⚠️ Em alta concorrência, transações serializáveis podem fazer retry
  — aceitável no MVP (operações de estoque são pouco frequentes)
