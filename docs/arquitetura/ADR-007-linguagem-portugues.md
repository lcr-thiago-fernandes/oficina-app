# ADR-007 — Código em português (linguagem ubíqua)

**Status:** Aceita
**Data:** 2026-05-03

## Contexto

Convenção em projetos .NET é nomear código em inglês. Mas DDD prescreve
**uma única linguagem ubíqua** compartilhada entre código, documentação e
conversas com o domínio. Domínio, equipe e entregáveis (Event Storming, vídeo)
estão em português.

## Decisão

Todos os identificadores de domínio em **português**: `Oficina.Dominio`,
`Cliente`, `OrdemDeServico`, `ItemServico`, `RegistrarEntrada`, etc. Apenas
termos técnicos do framework (`DbContext`, `IServiceCollection`) ficam em inglês.

## Consequências

- ✅ Linguagem ubíqua coerente — termo do código = termo do Event Storming
- ✅ Defesa do trabalho fica natural
- ⚠️ Foge da convenção .NET — pode ser estranho para devs externos
- ⚠️ Acentos e caracteres especiais nos nomes (`SaldoAtual`, `Orcamento`)
  — escolha consciente
