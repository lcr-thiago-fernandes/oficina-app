# Contratos de mensagens da Saga da OS

Fonte de verdade dos 16 tipos de mensagem trocados entre `oficina-app` (OS Service),
`oficina-billing-service` e `oficina-execucao-service`. Definição completa (produtor,
consumidor, comando × evento) na seção 5 de
[`docs/arquitetura/fase4-design-arquitetural.md`](../../arquitetura/fase4-design-arquitetural.md).

Regras:

- Cada arquivo `<Nome>.json` é o corpo da mensagem cujo URN MassTransit é `oficina:saga:<Nome>`.
- Serialização: `System.Text.Json` com `JsonSerializerDefaults.Web` (camelCase) e
  `JsonStringEnumConverter` (enum como string). Campos opcionais ausentes = `null`.
- **Estes arquivos são copiados byte a byte** para `docs/contratos/mensagens/` dos outros dois
  repositórios. Cada repositório tem um teste de contrato que, para cada tipo que produz ou
  consome, desserializa o JSON no seu tipo, re-serializa e compara (mesmo conjunto de
  propriedades e mesmos valores). Alterar um contrato exige PR nos três repositórios.
