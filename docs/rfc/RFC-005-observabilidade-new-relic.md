# RFC-005 — Plataforma de observabilidade: New Relic

**Status:** Aceita
**Data:** 2026-09-15
**Autor:** Thiago Fernandes
**ADRs relacionados:** ADR-013 (observabilidade mínima da Fase 2), ADR-020

## 1. Contexto e problema

**Estado real na data acima: a conta New Relic não existe, nada foi aplicado na AWS e
nenhum dado de observabilidade foi produzido em lugar nenhum.** `newrelic_habilitado =
false` no `oficina-infra-k8s` (`terraform/variables.tf`), `newrelic_layer_arn = null` no
`oficina-lambda-auth` (`terraform/variables.tf`), e o agente APM na imagem do
`oficina-app` vem desligado por padrão (`CORECLR_ENABLE_PROFILING=0`, `docker/Dockerfile`).
Este RFC descreve **o que está declarado em código e em Terraform** — alertas, dashboard,
instrumentação — e nunca o que se observou: não existe um dashboard exibindo nada, nem um
alerta que já disparou, porque não há conta para dispará-lo.

A Fase 2 ([ADR-013](../arquitetura/ADR-013-observabilidade-minima.md)) parou em métricas
OpenTelemetry expostas em `/metrics` (formato Prometheus) e logs estruturados em stdout via
Serilog — suficiente para o HPA (via `metrics-server`, não Prometheus) e para inspeção
manual (`kubectl`, `curl /metrics`), mas sem dashboards, sem alertas e sem tracing
distribuído. A Fase 3 exige mais: métricas de latência por endpoint, CPU/memória do
cluster, healthchecks/uptime, alertas quando o processamento de uma ordem de serviço falha,
e logs correlacionáveis ponta a ponta (API Gateway → Lambda Authorizer → API → banco). A
pergunta deste RFC é qual plataforma cobre esses cinco requisitos sem o time assumir a
operação de uma stack própria de monitoração, dado o orçamento de tempo e o tamanho do
cluster (dois nós `t3.medium`, [RFC-001](RFC-001-nuvem-aws.md)).

## 2. Alternativas consideradas

| Critério | New Relic — **escolhida** | Amazon CloudWatch + X-Ray | Prometheus + Grafana + Loki (auto-hospedados no cluster) |
|---|---|---|---|
| Custo real neste volume | Free tier de 100 GB/mês de ingestão cobre o projeto inteiro (dois ambientes, tráfego de demonstração) | Cobrança por métrica customizada, por trace do X-Ray e por GB de log — soma com o item já orçado em [RFC-001](RFC-001-nuvem-aws.md) | Sem custo de SaaS, mas consome CPU/memória do próprio cluster de 2 nós `t3.medium` que a aplicação precisa |
| Esforço de operação | Zero componente a mais para manter; SaaS gerenciado | Nativo da AWS, mas cada peça (dashboards do CloudWatch, grupos de log, traces do X-Ray) é configurada e mantida à parte | Operar três componentes a mais (Prometheus, Grafana, Loki) dentro do mesmo cluster que hospeda a API — scrape, retenção, storage, upgrade, tudo por conta do time |
| APM .NET pronto | Agente oficial para `dotnet8` (runtime gerenciado), instalação via tarball ou pacote, sem instrumentação manual de código | Não existe um "APM .NET" da AWS; X-Ray precisa do SDK instrumentado manualmente em cada chamada relevante | Instrumentação de APM .NET não é o que Prometheus/Grafana oferecem nativamente; exigiria bibliotecas de métricas customizadas por conta própria |
| Correlação log-trace automática | *Logs in context*: o agente injeta `trace.id`/`span.id` em cada linha de log sem código adicional | Exigiria instrumentação manual para amarrar `X-Ray trace ID` a cada linha de log do CloudWatch Logs | Loki não tem equivalente pronto a *logs in context*; correlação exigiria convenção manual de labels |
| Alertas como código em Terraform | Provider `newrelic` oficial (`newrelic_nrql_alert_condition`, `newrelic_one_dashboard`) | Terraform tem `aws_cloudwatch_metric_alarm`, mas dashboards de X-Ray/CloudWatch são menos expressivos para NRQL-like queries sobre eventos de negócio | Alertmanager como código existe, mas é mais um componente a versionar e operar dentro do cluster |

A stack auto-hospedada foi recusada porque o cluster desta oficina tem **dois nós
`t3.medium`** ([RFC-001](RFC-001-nuvem-aws.md), tabela de custo): rodar Prometheus, Grafana
e Loki nesses mesmos nós consome memória e CPU que o HPA
([ADR-017](../arquitetura/ADR-017-escalabilidade-hpa.md)) reserva para escalar a própria
API — o oposto do que a Fase 3 pede. CloudWatch + X-Ray perdeu porque a correlação
log-trace exigiria instrumentação manual em cada chamada, e não existe um agente de APM
.NET equivalente ao da New Relic para a AWS: a métrica de negócio (`OrdemServicoEvento`,
seção 3) teria que ser modelada como métrica customizada do CloudWatch, sem o mesmo
vocabulário de consulta (NRQL) usado nos alertas e no dashboard.

## 3. Decisão

Adotar **New Relic** em três frentes, cada uma condicionada a `newrelic_habilitado`/
`newrelic_layer_arn` e hoje **desligada**:

| Frente | Mecanismo | Repositório |
|---|---|---|
| API .NET | Agente APM oficial instalado por tarball no `docker/Dockerfile` (versão fixada `10.54.0`, hash SHA-256 verificado), com `CORECLR_ENABLE_PROFILING=0` na imagem — o Deployment do Kubernetes é quem liga (`=1`) | `oficina-app` |
| Cluster EKS | `nri-bundle` via Helm, atrás de `count = var.newrelic_habilitado ? 1 : 0` (`oficina-infra-k8s/terraform/helm.tf`) | `oficina-infra-k8s` |
| Lambdas de autenticação | Layer `NewRelicDotnet` anexada só quando `local.newrelic_habilitado` é verdadeiro (`newrelic_layer_arn != null`) (`oficina-lambda-auth/terraform/lambda-auth-api.tf`, `lambda-authorizer.tf`) | `oficina-lambda-auth` |

### Correlação

Duas peças, independentes uma da outra:

1. **`trace.id`/`span.id`** — injetados pelo agente APM diretamente nas linhas de log
   (*logs in context*), sem código da aplicação envolvido.
2. **`correlationId`** — `MiddlewareDeCorrelacao`
   (`oficina-app/src/Oficina.Api/Configuracao/MiddlewareDeCorrelacao.cs`), primeiro da
   pipeline: lê `X-Correlation-Id` do cliente; se ausente, usa `X-Amzn-RequestId` do API
   Gateway; se nenhum dos dois existir, gera um `Guid` novo. O valor entra no `LogContext`
   do Serilog (`LogContext.PushProperty("correlationId", correlationId)`) e volta no header
   da resposta.

### Decisão de não duplicar logs

O agente APM já encaminha os logs da própria aplicação; o Fluent Bit do `nri-bundle` roda
com `Exclude_Path` cobrindo os namespaces `oficina-*`
(`oficina-infra-k8s/terraform/helm-values/nri-bundle.yaml.tftpl`:
`Exclude_Path /var/log/containers/*_oficina-hml_*.log,/var/log/containers/*_oficina-prd_*.log`).
O comentário do próprio arquivo de valores registra o motivo: encaminhar as mesmas linhas
pelos dois caminhos causaria log duplicado e cobrança dobrada de ingestão. O Fluent Bit
deste Helm chart cuida só dos logs de sistema do cluster (eventos do kube-state-metrics,
containers fora dos namespaces da aplicação).

### Métricas de negócio

O custom event `OrdemServicoEvento` é publicado a partir da última entrada de
`os.historico_status`, na mesma transação de persistência da OS
([ADR-020](../arquitetura/ADR-020-historico-status.md);
[sequência de abertura de OS](../arquitetura/diagramas/sequencia-abertura-os.md)). Campos
reais (`EventoOrdemServico`,
`oficina-app/src/Oficina.Aplicacao/OrdensServico/Telemetria/EventoOrdemServico.cs`):
`numeroOs`, `statusAnterior` (`"(inicial)"` quando não há transição anterior),
`statusNovo`, `duracaoNoStatusSegundos` (nulo na primeira transição), `resultado`
(`"Sucesso"` ou `"Falha"`) e `unidade`. Em falha de processamento, `statusNovo` recebe o
valor sentinela `"NaoAplicavel"` — deliberadamente fora do vocabulário de
`StatusOrdemDeServico`, para que uma tentativa que falhou não seja contada pelas mesmas
consultas que filtram por `statusNovo` (por exemplo, o painel de volume diário, que conta
`statusNovo = 'Recebida'`). A publicação em si é `PublicadorEventoOsNewRelic`
(`oficina-app/src/Oficina.Infraestrutura/Telemetria/PublicadorEventoOsNewRelic.cs`), que
chama `NewRelic.Api.Agent.NewRelic.RecordCustomEvent` e nunca propaga uma falha de
telemetria para o chamador — qualquer exceção do agente vira `LogWarning` e a requisição
segue.

### Alertas e dashboard como código

Provider `newrelic` em root Terraform próprio (`oficina-infra-k8s/terraform/newrelic/`).
Cinco condições de alerta, **nomes exatos como estão no Terraform**
(`terraform/newrelic/alertas.tf`), todas na política `newrelic_alert_policy.oficina_prod`
(nome `"Oficina-Prod"`, `incident_preference = "PER_CONDITION"`):

| Nome (`name` do recurso) | Tipo | Condição NRQL | Threshold |
|---|---|---|---|
| `Oficina-API-Prod-Latencia-Critical` | `newrelic_nrql_alert_condition.latencia` | `SELECT percentile(duration, 95) * 1000 FROM Transaction WHERE appName = 'oficina-api'` | `critical`: acima de 500 (ms) por 300 s |
| `Oficina-API-Prod-TaxaErro-Critical` | `.taxa_erro` | `SELECT percentage(count(*), WHERE error IS true) FROM Transaction WHERE appName = 'oficina-api'` | `critical`: acima de 5 (%) por 300 s |
| `Oficina-API-Prod-Uptime-Critical` | `.uptime` | `SELECT count(*) FROM Transaction WHERE appName = 'oficina-api'` | `critical`: abaixo de 1 por 300 s (perda de sinal — `expiration_duration = 300`, `open_violation_on_expiration = true`) |
| `Oficina-OS-Prod-FalhaProcessamento-Critical` | `.falha_os` | `SELECT count(*) FROM OrdemServicoEvento WHERE resultado = 'Falha'` | `critical`: acima de 0, `threshold_occurrences = "at_least_once"`, `threshold_duration = 60` |
| `Oficina-EKS-Prod-CPU-Warning` | `.cpu_nos` | `SELECT average(cpuUsedCores / allocatableCpuCores) * 100 FROM K8sNodeSample WHERE clusterName = 'oficina-eks'` | `warning`: acima de 80 (%) por 600 s |

Três condições — `latencia`, `taxa_erro` e `uptime` — filtram por `appName =
'oficina-api'` (contrato de nome com o `oficina-app` — `NEW_RELIC_APP_NAME` fixo em
`"oficina-api"`, sem sufixo de ambiente, `k8s/deployment.yaml`).
`Oficina-OS-Prod-FalhaProcessamento-Critical` não filtra por `appName`, porque é um
evento customizado com nome próprio e já único da aplicação (comentário do próprio
`alertas.tf`). `Oficina-EKS-Prod-CPU-Warning` também não filtra por `appName`: é métrica
de nó do cluster (`K8sNodeSample`), filtrada por `clusterName`, não por aplicação. Cada
condição carrega `runbook_url` apontando para uma âncora de
`oficina-infra-k8s/docs/runbooks.md`.

Um dashboard (`newrelic_one_dashboard.oficina`, nome `"Oficina Mecanica - Operacao"`,
`permissions = "public_read_only"`), seis widgets, **nomes exatos como estão no
Terraform** (`terraform/newrelic/dashboard.tf`):

| Widget (tipo Terraform) | Título |
|---|---|
| `widget_line` | "Volume diario de OS" — `count(*) FROM OrdemServicoEvento WHERE statusNovo = 'Recebida'` |
| `widget_bar` | "Tempo medio por status (minutos)" — `average(duracaoNoStatusSegundos)/60 FACET statusAnterior` |
| `widget_line` | "Latencia p95 por endpoint (ms)" — `percentile(duration, 95) * 1000 FROM Transaction FACET name` |
| `widget_billboard` | "Taxa de erro (%)" — `percentage(count(*), WHERE error IS true) FROM Transaction` (limiares `warning = 2`, `critical = 5`) |
| `widget_line` | "CPU e memoria dos pods (oficina-*)" — `average(cpuUsedCores)`/`average(memoryWorkingSetBytes)` de `K8sContainerSample` |
| `widget_table` | "Saude dos pods" — `latest(status)`/`latest(restartCount)`/`latest(isReady)` de `K8sContainerSample` |

Os **cinco nomes de condição de alerta** (`name` do recurso) coincidem, palavra por
palavra (a menos de acentuação, removida no `name`), com os cinco nomes listados na
tabela da seção 5 do design (`fase3-design-arquitetural.md`). Os **títulos dos
widgets** não são uma cópia literal: o design lista os seis painéis em prosa ("volume
diário de OS", "tempo médio por status", "latência por endpoint", "taxa de erro",
"CPU/memória dos pods", "saúde dos pods"), e os títulos do Terraform
(`terraform/newrelic/dashboard.tf`) são elaborações dessa lista — por exemplo,
"Latencia p95 por endpoint (ms)" precisa a métrica (`p95`) e a unidade (`ms`) que a
prosa do design não especifica. Não há divergência de conteúdo a registrar aqui: o que
muda entre design e implementação está isolado nas seções V12 e "defeito de
telemetria" abaixo. Notificação por e-mail: `newrelic_notification_destination.email`
(`"Oficina-Email"`) → `newrelic_notification_channel.email` (`"Oficina-Email-Canal"`,
produto `IINT`) → `newrelic_workflow.oficina_prod` (`"Oficina-Prod-Workflow"`,
`muting_rules_handling = "NOTIFY_ALL_ISSUES"`), filtrando por
`labels.policyIds EXACTLY_MATCHES [newrelic_alert_policy.oficina_prod.id]`
(`terraform/newrelic/notificacoes.tf`).

### V12 — root separado, desligado, segredo sempre presente

O provider `newrelic` (`terraform/newrelic/providers.tf`) exige `account_id` e `api_key`
como argumentos de configuração do provider — ambos sem valor padrão em
`terraform/newrelic/variables.tf` (`newrelic_account_id` e `newrelic_api_key`, este último
`sensitive`). Um provider sem valor válido para essas duas variáveis falha na
inicialização (`terraform init`/`plan`) de **todo** o root que o declarar, não só dos
recursos que o usam — por isso este root é próprio
(`terraform/newrelic/backend.tf`: `key = "infra-k8s-newrelic/terraform.tfstate"`, mesmo
bucket `oficina-tfstate-fiap-15soat` dos demais states), aplicável só depois que a conta
New Relic existir e as credenciais forem fornecidas, sem bloquear o `plan`/`apply` da VPC,
do EKS ou do NLB no root principal.

Hoje está **desligado**: `newrelic_habilitado` tem `default = false`
(`oficina-infra-k8s/terraform/variables.tf`), e `newrelic_layer_arn` tem `default = null`
(`oficina-lambda-auth/terraform/variables.tf`) — `local.newrelic_habilitado =
var.newrelic_layer_arn != null` (`oficina-lambda-auth/terraform/locals.tf`) resolve para
falso.

O segredo `oficina/newrelic_license_key` **sempre existe**: `aws_secretsmanager_secret.
newrelic_license_key` (`oficina-infra-k8s/terraform/secrets.tf`) não tem `count` nem
condicional — é criado incondicionalmente, com o comentário do próprio arquivo confirmando
o motivo: "Criado SEMPRE (decisão D2): o CD do `oficina-app` falha se este segredo não
existir." O valor gravado é `var.newrelic_habilitado ? var.newrelic_license_key :
local.newrelic_placeholder`, e `local.newrelic_placeholder = "NEW-RELIC-DESLIGADO"`
(`oficina-infra-k8s/terraform/locals.tf`). Com o placeholder, o agente .NET recebe uma
licença inválida, se recusa a reportar e se desliga sozinho — a API sobe normalmente
(comentário de `secrets.tf`).

### Defeito de telemetria já corrigido: filtro do `catch`

Uma revisão anterior identificou que o `catch` que classifica falhas de processamento era
irrestrito: qualquer exceção — incluindo um erro de negócio do cliente (por exemplo, um
422 de transição de status inválida) — publicaria `OrdemServicoEvento` com `resultado =
'Falha'`, disparando o alerta obrigatório `Oficina-OS-Prod-FalhaProcessamento-Critical` a
cada erro 4xx legítimo do cliente, até o alerta virar ruído e ser ignorado. No código
atual, o filtro está restrito: `PublicadorEventoOsExtensions.EhFalhaDeProcessamento`
(`oficina-app/src/Oficina.Aplicacao/OrdensServico/Telemetria/PublicadorEventoOsExtensions.cs`,
linhas 28-29) classifica como falha de processamento **tudo que não seja**
`ExcecaoDeDominio` nem `OperationCanceledException`; o `catch` que publica o evento de
falha (linha 44) usa exatamente esse filtro:
`catch (Exception ex) when (EhFalhaDeProcessamento(ex))`. `ExcecaoDeDominio`
(`oficina-app/src/Oficina.Dominio/ExcecaoDeDominio.cs`) é a raiz de toda exceção de regra
de negócio mapeada para 4xx pelo `MiddlewareDeExcecoes` — inclusive as que viram 422
(`TransicaoDeStatusInvalidaException`, `OrdemInvalidaException` e as demais listadas em
`oficina-app/src/Oficina.Api/Configuracao/MiddlewareDeExcecoes.cs`) — e o próprio
comentário da classe já documenta o propósito: "um 422 de transição inválida não pode
disparar alerta de produção". Hoje, um erro de negócio do cliente sobe sem publicar evento
de falha nenhum; só uma exceção fora dessas duas categorias (banco fora do ar, bug,
timeout) alimenta `resultado = 'Falha'` e, por consequência, o alerta Critical.

### SLI/SLO e synthetic monitoring — fora de escopo

`newrelic_service_level` (SLI/SLO como código) e monitores sintéticos
(`newrelic_synthetics_monitor`) ficaram fora do escopo desta fase: não há recurso
Terraform correspondente em `terraform/newrelic/`. Conceitualmente, um SLI de
disponibilidade poderia ser definido sobre a mesma condição de uptime (`Transaction`
`count(*)` por janela) e um SLO de latência sobre o mesmo p95 já alertado — mas formalizar
os dois como `newrelic_service_level` (que produz os eventos `ServiceLevelIndicatorEvent`
consumidos por relatórios de erro-budget) e adicionar monitores sintéticos batendo em
`/health` de fora da AWS exigiria decidir metas de erro-budget e frequência de execução dos
synthetics — decisões de produto que não foram tomadas nesta fase. Fica registrado como
extensão natural, não como lacuna silenciosa.

## 4. Consequências

- ✅ Os cinco requisitos de observabilidade da Fase 3 (latência, CPU/memória, healthchecks,
  alerta de falha no processamento de OS, logs correlacionados) estão cobertos por
  configuração declarada, sem o time operar uma stack própria de monitoração no cluster.
- ✅ Alertas e dashboard são código versionado (`terraform/newrelic/`), revisável por PR
  como qualquer outro recurso do repositório, não configuração manual feita direto na
  console do New Relic.
- ✅ Uma requisição é rastreável ponta a ponta por `correlationId` (gerado pelo
  `MiddlewareDeCorrelacao`, propagado no header de resposta) e por `trace.id`/`span.id`
  (injetados pelo agente, quando ligado).
- ⚠️ Dependência de um SaaS externo e do seu free tier: ultrapassar 100 GB/mês de ingestão
  passa a ter custo, e a plataforma inteira depende da disponibilidade do New Relic.
- ⚠️ Enquanto `newrelic_habilitado = false` (e `newrelic_layer_arn = null`), nada disso
  produz um único dado real: não há métrica, não há log correlacionado por `trace.id`, não
  há alerta que possa disparar. Ligar exige, nesta ordem, criar a conta, obter
  `account_id`/`api_key`/license key reais, e rodar um novo `apply` do root
  `terraform/newrelic/` e dos roots que condicionam a instrumentação a
  `newrelic_habilitado`/`newrelic_layer_arn`.
- ⚠️ Ao ligar, os valores do Fluent Bit no `nri-bundle` (`Exclude_Path` dos namespaces
  `oficina-*`) e o schema do provider `newrelic` (versão `3.97.5` travada no lockfile do
  root `terraform/newrelic/`) precisam ser revisados contra a conta real — nenhum dos dois
  foi validado contra uma ingestão de dados de verdade.
- ⚠️ SLI/SLO como código (`newrelic_service_level`) e synthetic monitors ficaram fora de
  escopo (seção 3) — não há recurso Terraform para nenhum dos dois.

## 5. Como isto está implementado

| O quê | Onde |
|---|---|
| Agente APM .NET na imagem, versão fixada e hash verificado, desligado por padrão | `oficina-app/docker/Dockerfile` |
| Profiler ligado no Deployment (`CORECLR_ENABLE_PROFILING=1`), `NEW_RELIC_APP_NAME` fixo, licença via Secret | `oficina-app/k8s/deployment.yaml` |
| Chave `NewRelic__LicenseKey` do Secret consumida como `NEW_RELIC_LICENSE_KEY` | `oficina-app/k8s/secret.yaml` |
| Middleware de correlação (`X-Correlation-Id` / `X-Amzn-RequestId` / novo `Guid`, `LogContext`) | `oficina-app/src/Oficina.Api/Configuracao/MiddlewareDeCorrelacao.cs` |
| Evento de negócio `OrdemServicoEvento` (campos, valor sentinela de falha) | `oficina-app/src/Oficina.Aplicacao/OrdensServico/Telemetria/EventoOrdemServico.cs` |
| Publicação do evento (sucesso/falha) e o filtro `EhFalhaDeProcessamento` do defeito corrigido | `oficina-app/src/Oficina.Aplicacao/OrdensServico/Telemetria/PublicadorEventoOsExtensions.cs` |
| Raiz `ExcecaoDeDominio` (o que distingue erro de negócio de falha de processamento) | `oficina-app/src/Oficina.Dominio/ExcecaoDeDominio.cs` |
| Publicador concreto do custom event no New Relic (`RecordCustomEvent`) | `oficina-app/src/Oficina.Infraestrutura/Telemetria/PublicadorEventoOsNewRelic.cs` |
| Mapeamento de exceções para 422 (entre outros) | `oficina-app/src/Oficina.Api/Configuracao/MiddlewareDeExcecoes.cs` |
| Root Terraform do New Relic (state próprio, decisão de root separado) | `oficina-infra-k8s/terraform/newrelic/backend.tf`, `providers.tf`, `variables.tf` |
| Cinco condições de alerta | `oficina-infra-k8s/terraform/newrelic/alertas.tf` |
| Dashboard de seis widgets | `oficina-infra-k8s/terraform/newrelic/dashboard.tf` |
| Canal de notificação e workflow | `oficina-infra-k8s/terraform/newrelic/notificacoes.tf` |
| `newrelic_habilitado` (default `false`) e segredo sempre criado com placeholder (V12) | `oficina-infra-k8s/terraform/variables.tf`, `oficina-infra-k8s/terraform/secrets.tf`, `oficina-infra-k8s/terraform/locals.tf` |
| `nri-bundle` condicionado a `newrelic_habilitado`, valores do Fluent Bit (`Exclude_Path`) | `oficina-infra-k8s/terraform/helm.tf`, `oficina-infra-k8s/terraform/helm-values/nri-bundle.yaml.tftpl` |
| `newrelic_layer_arn` (default `null`) e instrumentação condicional das Lambdas | `oficina-lambda-auth/terraform/variables.tf`, `oficina-lambda-auth/terraform/locals.tf`, `oficina-lambda-auth/terraform/lambda-auth-api.tf`, `oficina-lambda-auth/terraform/lambda-authorizer.tf` |
| Runbooks referenciados por cada condição de alerta | `oficina-infra-k8s/docs/runbooks.md` |
