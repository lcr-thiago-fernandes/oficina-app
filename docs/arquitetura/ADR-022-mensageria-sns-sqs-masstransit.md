# ADR-022 — Mensageria: Amazon SNS + SQS via MassTransit 8.x

**Status:** Aceita
**Data:** 2026-11-17
**RFC relacionado:** [RFC-006](../rfc/RFC-006-microsservicos-saga-orquestrada.md)

## Contexto

A saga da OS ([ADR-021](ADR-021-saga-orquestrada-masstransit.md)) troca 16 tipos de mensagem
entre três repositórios .NET. O transporte precisa ser gerenciado (o projeto não quer operar
um broker no EKS), barato, executável localmente e testável sem infraestrutura no CI.

## Decisão

- **Transporte:** Amazon SNS (um tópico por tipo de evento) + Amazon SQS (uma fila por
  consumidor), via **MassTransit 8.x** (Apache-2.0; a v9 tem licença comercial e não é usada).
- **Versões:** o OS Service usa **8.4.1** em `MassTransit`, `MassTransit.AmazonSQS` e
  `MassTransit.EntityFrameworkCore`. A partir da 8.5.0 o `MassTransit.EntityFrameworkCore`
  passa a exigir EF Core 9 (`Microsoft.EntityFrameworkCore.Relational 9.0.1`) no `net8.0`,
  incompatível com o EF Core/Npgsql 8.0.10 do serviço. O Dependabot ignora `MassTransit*`
  `>= 8.5.0` (`.github/dependabot.yml`). Billing e Execução, que não usam EF Core, estão na
  **8.5.12**. O envelope é o mesmo; a interoperabilidade é garantida pelos testes de contrato e
  pelo fluxo ponta a ponta local.
- **Transporte por configuração:** `Mensageria__Transporte` = `AmazonSqs` (Kubernetes,
  `k8s/configmap.yaml`) ou `InMemory` (padrão do `appsettings.json`; testes usam o test
  harness). `Mensageria__ServiceUrl` aponta para o LocalStack no ambiente local.
- **Nomes independentes do namespace .NET:** cada contrato leva
  `[MessageUrn("oficina:saga:<Nome>")]` e `[EntityName("oficina-saga-<Nome>")]`. O prefixo do
  ambiente (`Mensageria__PrefixoAmbiente` = `local` | `hml` | `prd`) é aplicado **por tipo**
  em `TopologiaSaga.Aplicar` (`cfg.Message<T>(m => m.SetEntityName(...))`), porque um tipo com
  `[EntityName]` ignora o formatter global. Resultado: tópico `{prefixo}-oficina-saga-{Nome}`,
  filas `{prefixo}-oficina-billing-comandos`, `{prefixo}-oficina-execucao-comandos`,
  `{prefixo}-oficina-os-saga` e a fila de bus por réplica `{prefixo}-oficina-os-bus-{HOSTNAME}`
  (`OverrideDefaultBusEndpointQueueName`, truncada em 80 caracteres). Tudo casa com a política
  IAM do IRSA (`terraform/iam-irsa.tf`, recursos `{amb}-oficina-*`).
- `PublishFaults = false` em todo receive endpoint: os tópicos `Fault<T>` seriam nomeados pelo
  tipo .NET, fora da política IAM; a falha definitiva vai para a fila `_error` do endpoint.
- Retry: `UseMessageRetry` exponencial (3 tentativas, 200 ms → 5 s) ignorando
  `ExcecaoDeDominio`, dentro de `UseDelayedRedelivery` (30 s, 2 min, 5 min).

## Alternativas

- **RabbitMQ no cluster** — usado nas aulas, mas exigiria operar um StatefulSet no EKS.
- **Kafka/MSK** — caro e desproporcional ao volume.

## Consequências

- ✅ Sem broker para operar; custo praticamente zero; LocalStack para o fluxo local.
- ✅ Nomes finais provados por teste: `DependencyInjectionMensageriaTestes`
  (`Topologia_ComPrefixoHml_NomeFinalDoTopicoDeCadaTipo`) e `TopologiaLocalStackTestes` (bus
  real contra LocalStack em Testcontainers).
- ⚠️ SQS é *at-least-once* e sem ordem garantida (fila padrão): todo consumidor precisa ser
  idempotente e tolerar mensagem adiantada ([ADR-024](ADR-024-outbox-e-idempotencia.md)).
- ⚠️ Duas versões de MassTransit convivem; subir o OS Service para 8.5+ depende de migrar para
  EF Core 9.
