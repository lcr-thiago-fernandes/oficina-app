# RFC-006 — Microsserviços e Saga orquestrada

**Status:** Aceita
**Data:** 2026-11-17
**Autor:** Thiago Fernandes
**Design completo:** [fase4-design-arquitetural.md](../arquitetura/fase4-design-arquitetural.md)
**ADRs relacionados:** [ADR-021](../arquitetura/ADR-021-saga-orquestrada-masstransit.md),
[ADR-022](../arquitetura/ADR-022-mensageria-sns-sqs-masstransit.md),
[ADR-023](../arquitetura/ADR-023-bancos-por-servico.md),
[ADR-024](../arquitetura/ADR-024-outbox-e-idempotencia.md),
[ADR-025](../arquitetura/ADR-025-reserva-estoque-semantic-lock.md),
[ADR-026](../arquitetura/ADR-026-sonarqube-efemero-no-ci.md)

## 1. Contexto e problema

A oficina passou a operar em escala nacional, com várias filiais. Na Fase 3 a API
(`oficina-app`) era um monólito modular: atendimento, orçamento, aprovação e execução da OS
no mesmo processo e no mesmo banco, com a aprovação do orçamento chegando por um webhook
autenticado por token ([ADR-011](../arquitetura/ADR-011-webhook-aprovacao-token.md)). A Fase 4
pede **pelo menos três microsserviços** — cada um com repositório, infraestrutura, banco e
pipeline próprios, sem acesso ao banco do outro — e uma **Saga** com compensação em qualquer
etapa que pode falhar, incluindo uma integração real de pagamento (Mercado Pago).

A pergunta deste RFC: como dividir o sistema e como coordenar uma transação que atravessa
serviços sem transação distribuída, sem virar um "monolito distribuído" e sem compensações
que não compensam.

## 2. Alternativas consideradas

### 2.1 Divisão em serviços

| Opção | Avaliação |
|---|---|
| **(a) OS, Billing e Execução** (sugeridos no enunciado) ✅ | Mapeia direto nas áreas do desafio (atendimento, cobrança, execução); o Billing isola a integração externa (Mercado Pago) |
| (b) Separar também Clientes, Catálogo e Estoque | 5–6 serviços para um projeto solo; mais repositórios do que o domínio pede agora |

### 2.2 Coordenação da transação

| Opção | Prós | Contras |
|---|---|---|
| (a) Coreografia | Sem ponto central; acoplamento fraco | Fluxo espalhado em 3 repositórios; dependência cíclica (OS → Billing → OS → Execução → Billing para o estorno); difícil de explicar e de testar ponta a ponta |
| **(b) Orquestração com MassTransit State Machine no OS Service** ✅ | Fluxo e compensações em um lugar legível; participantes não conhecem a saga; dependências em mão única; testável com o test harness | O orquestrador é peça crítica (mitigado: estado persistido no PostgreSQL + outbox, réplicas no K8s) |
| (c) AWS Step Functions | Gerenciado, visual | Lógica fora do código .NET, difícil de cobrir com testes/BDD no CI, custo e acoplamento à AWS |
| (d) Chamadas REST síncronas em cadeia | Simples de ler | Exatamente o "monolito distribuído": um serviço fora do ar trava o fluxo inteiro; sem compensação |

## 3. Decisão

1. **Três microsserviços:** `oficina-app` vira o **OS Service** (o repositório não foi
   renomeado, para manter válidos os links da Fase 3); `oficina-billing-service` (orçamento e
   pagamento) e `oficina-execucao-service` (fila de execução, diagnóstico, reparo) são novos.
   Clientes, veículos, catálogo e estoque **permanecem no OS Service** (contexto de
   atendimento e origem dos preços).
2. **Saga orquestrada** dentro do OS Service ([ADR-021](../arquitetura/ADR-021-saga-orquestrada-masstransit.md)).
   Justificativa (Aula 2, Richardson): a saga da OS tem 4 participantes lógicos, 3 pontos de
   falha e compensações em cascata — fora das sagas simples, a orquestração tende a ser mais
   benéfica.
3. **Mensageria assíncrona SNS/SQS** com MassTransit 8.x para comandos e eventos
   ([ADR-022](../arquitetura/ADR-022-mensageria-sns-sqs-masstransit.md)); **REST síncrono** só
   na borda (atendente, cliente, mecânico e webhook do Mercado Pago, pelo API Gateway).
4. **Banco por serviço:** PostgreSQL no OS Service, DynamoDB no Billing e na Execução
   ([ADR-023](../arquitetura/ADR-023-bancos-por-servico.md)).
5. **Falta do "I" de ACID tratada explicitamente:** reserva de estoque como *semantic lock*
   ([ADR-025](../arquitetura/ADR-025-reserva-estoque-semantic-lock.md)), versão otimista
   (`xmin` / `versao` condicional), outbox no OS Service e idempotência nos participantes
   ([ADR-024](../arquitetura/ADR-024-outbox-e-idempotencia.md)).
6. **Contratos de mensagem sem pacote compartilhado:** cada repositório declara as próprias
   classes com o mesmo URN (`oficina:saga:<Nome>`) e o mesmo nome de entidade
   (`oficina-saga-<Nome>`); os 16 JSONs de exemplo em `docs/contratos/mensagens/` são idênticos
   nos três repositórios e verificados por teste de contrato no CI de cada um.
7. **Infraestrutura própria por serviço:** cada serviço tem `terraform/` e `infra.yml` próprios.
   No OS Service, o Terraform cria só a role IRSA por ambiente (SNS/SQS `{amb}-oficina-*`) e o
   parâmetro `/oficina/os-service/<amb>/irsa_role_arn`; o contrato com `oficina-infra-k8s`
   continua por SSM ([RFC-004](RFC-004-topologia-quatro-repositorios.md)).

## 4. Consequências

- ✅ Cada serviço é implantado, escalado e testado de forma independente; uma falha do Billing
  ou da Execução não derruba a abertura de OS (a saga espera, com retry/redelivery).
- ✅ Toda etapa que pode falhar tem compensação explícita, testada no test harness e no BDD.
- ✅ A aprovação do orçamento deixou de ser um webhook com token compartilhado no OS Service
  ([ADR-011](../arquitetura/ADR-011-webhook-aprovacao-token.md), agora *superseded*): o cliente
  aprova no Billing (rota autenticada pelo Lambda Authorizer) e o webhook público é o do Mercado
  Pago, validado por assinatura `x-signature` (HMAC-SHA256).
- ⚠️ Consistência eventual: entre a abertura e a resposta do Billing a OS fica em `Recebida`; o
  `GET /api/v1/ordens-servico/{id}/saga` mostra o estado e a trilha.
- ⚠️ O contrato de mensagens falha em silêncio se divergir (nome de fila, URN, campo); mitigado
  pelos testes de contrato e de topologia, não eliminado — alterar um contrato exige PR nos
  três repositórios.
- ⚠️ `EstornoFalhou` exige intervenção manual (saga em `Compensando`, alerta Critical); não há
  timeout de aprovação (não-objetivo).
- ⚠️ A [RFC-004](RFC-004-topologia-quatro-repositorios.md) tratava o `oficina-app` como
  consumidor do contrato SSM, sem Terraform próprio. Na Fase 4 ele passa a ter um `terraform/`
  (role IRSA e o parâmetro `/oficina/os-service/<amb>/irsa_role_arn`), aplicado pelo
  `infra.yml` com a role compartilhada `oficina-gha-infra`.

## 5. Como isto está implementado

| O quê | Onde |
|---|---|
| Máquina de estados da saga | `oficina-app/src/Oficina.Infraestrutura/Mensageria/Saga/OrdemServicoSaga.cs` |
| Efeitos locais de cada passo | `oficina-app/src/Oficina.Aplicacao/OrdensServico/Saga/PassosSagaOrdemServico.cs` |
| Nomes de filas/tópicos e topologia | `oficina-app/src/Oficina.Infraestrutura/Mensageria/{NomesMensageria,TopologiaSaga}.cs` |
| Outbox, retry e redelivery | `oficina-app/src/Oficina.Infraestrutura/Mensageria/DependencyInjectionMensageria.cs` |
| Contratos (JSON) | `docs/contratos/mensagens/*.json` (nos três repositórios) |
| BDD do fluxo completo | `oficina-app/tests/Oficina.Bdd.Testes/Funcionalidades/FluxoCompletoOrdemServico.feature` |
| Role IRSA do OS Service | `oficina-app/terraform/iam-irsa.tf`, `oficina-app/terraform/ssm.tf` |
| Billing (orçamento, pagamento, Mercado Pago) | [`oficina-billing-service`](https://github.com/lcr-thiago-fernandes/oficina-billing-service) |
| Execução (fila, diagnóstico, reparo) | [`oficina-execucao-service`](https://github.com/lcr-thiago-fernandes/oficina-execucao-service) |
| SSM novos, rotas do API Gateway, dashboard/alertas da saga, alarmes de DLQ | [`oficina-infra-k8s`](https://github.com/lcr-thiago-fernandes/oficina-infra-k8s) (PR #21) |
