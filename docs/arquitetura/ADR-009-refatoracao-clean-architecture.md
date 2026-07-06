# ADR-009 — Refatoração para Clean Architecture (Controllers/Gateways/Presenters/DataSources)

**Status:** Aceita
**Data:** 2026-07-06

## Contexto

Na Fase 1 o sistema era Clean Architecture com 4 projetos (`Api`, `Aplicacao`, `Dominio`,
`Infraestrutura`), mas os controllers ASP.NET falavam direto com casos de uso e o
mapeamento entrada/saída ficava espalhado. A Fase 2 pede maior separação de
responsabilidades e testabilidade dos adaptadores de interface, no estilo apresentado
na disciplina (Controllers/Gateways/Presenters/DataSources).

## Decisão

Introduzimos um **5º projeto, `Oficina.Adaptadores`** (camada de Adaptadores de Interface),
entre `Api` e `Aplicacao`, com quatro blocos por bounded context:

- **Controllers de aplicação** — orquestram os casos de uso e formatam a saída via Presenter
  (ex.: `OrdemDeServicoController.AbrirAsync`). Não confundir com os controllers ASP.NET.
- **Gateways** — implementam as **portas Gateway** declaradas em `Oficina.Aplicacao`
  (ex.: `IOrdemDeServicoGateway`), apoiando-se em DataSources.
- **Presenters** — convertem agregados do domínio em DTOs de resposta.
- **DataSources** — **portas de acesso a dados** (interfaces, ex.: `IOrdemDeServicoDataSource`)
  implementadas na Infraestrutura. Regras de consulta puras (filtro/ordenação) ficam em
  classes testáveis (`OrdemDeServicoQuery`).

Regra de dependência (aponta sempre para dentro):

- `Dominio` → nada
- `Aplicacao` → `Dominio` (e define as portas Gateway)
- `Adaptadores` → `Aplicacao` (implementa portas; define portas DataSource)
- `Infraestrutura` → `Dominio` + `Aplicacao` + `Adaptadores` (implementa DataSources, repositórios e serviços)
- `Api` → `Aplicacao` + `Infraestrutura` + `Adaptadores` (apenas wiring de DI)

Os controllers ASP.NET em `Oficina.Api` ficam **finos**: recebem o request, delegam ao
Controller de aplicação (injetado via DI) e mapeiam o resultado para `IActionResult`.

## Consequências

- ✅ Separa claramente entrada/saída (Presenter) do fluxo de negócio (Use Case)
- ✅ Adaptadores testáveis isoladamente (Gateways/Query sem ASP.NET)
- ✅ Portas explícitas (Gateway na Aplicação, DataSource nos Adaptadores) reforçam a inversão de dependência
- ⚠️ Mais projetos e boilerplate (interfaces + implementações) que a versão de 4 camadas
- ⚠️ A Infraestrutura passa a referenciar `Adaptadores` para implementar os DataSources — dependência aceitável por estar na borda externa
