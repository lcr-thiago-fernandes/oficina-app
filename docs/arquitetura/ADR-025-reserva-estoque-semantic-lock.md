# ADR-025 — Reserva de estoque como *semantic lock*

**Status:** Aceita
**Data:** 2026-11-17
**RFC relacionado:** [RFC-006](../rfc/RFC-006-microsservicos-saga-orquestrada.md)
**ADRs relacionados:** [ADR-006](ADR-006-saldo-denormalizado-estoque.md) (saldo desnormalizado)

## Contexto

Na Fase 3 o estoque era baixado ao iniciar a execução, dentro de uma transação local. Na
Fase 4, entre a abertura da OS e o início da execução existem orçamento, decisão do cliente e
pagamento — minutos ou dias, em outros serviços. Uma saga não tem o "I" de ACID: se o estoque
fosse baixado na abertura e devolvido numa compensação, outra OS poderia ler, nesse intervalo,
um saldo que ainda vai voltar (ou vender uma peça que não existe mais). Se não fosse tocado
até o pagamento, duas OS pagariam pela mesma última unidade.

## Decisão

Contramedida *semantic lock* (Richardson, Aula 3): o estoque passa por **reservado →
confirmado** ou **reservado → liberado**, com saldo reservado separado do disponível.

- `Peca` ganha `SaldoReservado` e `SaldoDisponivel = SaldoAtual - SaldoReservado`
  (`src/Oficina.Dominio/Estoque/Peca.cs`), com *check constraint*
  `saldo_reservado >= 0 AND saldo_reservado <= saldo_atual`.
- Movimentações novas: `Reserva`, `LiberacaoReserva`, `Reposicao` (além de `Entrada`/`Saida`).
  A situação da reserva de uma OS numa peça (`SituacaoReservaPeca`: `Nenhuma`, `Reservada`,
  `Confirmada`, `Liberada`, `Reposta`) é derivada do último movimento daquela OS.
- **Abertura (passo 1):** `Reservar` dentro da transação `Serializable` da abertura; saldo
  disponível insuficiente → 422 síncrono e a saga nem começa (nada vai para o outbox).
- **Pagamento confirmado (passo 5):** `ConfirmarReserva` baixa o saldo e zera o reservado da OS
  (tudo ou nada: se alguma reserva já foi desfeita, nada é baixado e a saga estorna).
- **Compensação:** reserva ainda `Reservada` → `LiberarReserva`; já `Confirmada` → `Repor`.
  Repetir uma compensação devolve `null` (sem segundo movimento).
- **Saída manual** (`POST /api/v1/pecas/{id}/movimentacoes`, `Saida`) só consome o saldo
  disponível: as unidades reservadas são intocáveis (422).
- Concorrência entre aberturas e entre a saga e a saída manual: versão otimista (`xmin`) na
  `Peca` e transação `Serializable` na abertura; quem perde recebe 409/422 ou é retentado
  (consumo da saga).

## Consequências

- ✅ Nenhuma OS lê saldo que pode ser devolvido; o saldo nunca fica negativo nem reservado
  acima do saldo.
- ✅ Provado por teste: `ReservaEstoqueTestes` (domínio: `Reservar_AlemDoDisponivel_*`,
  `RegistrarSaida_AlemDoDisponivel_Lanca`, `Repor_Repetido_RetornaNull`,
  `LiberarReserva_Repetida_RetornaNull`) e `ReservaConcorrenteTestes` (PostgreSQL real:
  `AbrirOrdens_Concorrentes_PelaUltimaUnidade_SoUmaReserva`,
  `SaidaManual_SobreEstoqueReservado_Retorna422`).
- ⚠️ Peças ficam presas enquanto a OS aguarda aprovação/pagamento. Sem timeout de aprovação
  (não-objetivo), a liberação depende de rejeição, recusa de pagamento ou cancelamento manual.
- ⚠️ OS abertas na Fase 3 (sem reserva) não são compensadas no estoque automaticamente: o
  cancelamento registra aviso em log em vez de repor uma baixa manual legada.
