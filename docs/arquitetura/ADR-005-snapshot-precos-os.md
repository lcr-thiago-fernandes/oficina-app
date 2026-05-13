# ADR-005 — Snapshot de nome e preço nos itens de OS

**Status:** Aceita
**Data:** 2026-05-03

## Contexto

Quando um item (serviço ou peça) é incluído numa OS, ele referencia o catálogo.
Se o preço do catálogo mudar depois, OSs antigas não devem ser alteradas — o
cliente já viu/aprovou aquele valor.

## Decisão

`ItemServico` e `ItemPeca` armazenam **cópia** (snapshot) do `nome` e do `preco`
no momento da inclusão (`PrecoSnapshot`, `ServicoNome`, `PecaNome`). Após
gravados, mudanças no `Servico` ou `Peca` não afetam OSs já criadas.

## Consequências

- ✅ Histórico financeiro fiel (essencial para fechamentos e auditoria)
- ✅ Cliente vê o orçamento que aprovou
- ⚠️ Renomear um serviço no catálogo não atualiza OSs antigas (intencional)
- ⚠️ Pequena duplicação de dado (nome e preço); compensa pela imutabilidade
