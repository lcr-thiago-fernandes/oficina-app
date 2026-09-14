# Manifestos Kubernetes — Oficina API

Orquestração da API no EKS (Fase 3). O endpoint do RDS PostgreSQL, o `metrics-server`
e os segredos reais são provisionados pelo Terraform / CI. Um único cluster hospeda os
dois ambientes, em namespaces separados: `oficina-hml` (branch `develop`) e `oficina-prd`
(branch `main`).

## Arquivos

| Arquivo | Recurso (apiVersion) | O que cria |
|---|---|---|
| `namespace-hml.yaml` | Namespace (`v1`) | Namespace `oficina-hml` (homologação). |
| `namespace-prd.yaml` | Namespace (`v1`) | Namespace `oficina-prd` (produção). |
| `configmap.yaml` | ConfigMap (`v1`) | `oficina-api-config`: config não-sensível (`ASPNETCORE_ENVIRONMENT`, `Jwt__Issuer`, `Jwt__Audience`, `Bootstrap__ExecutarNoStartup=false`). |
| `secret.yaml` | Secret (`v1`) | `oficina-api-secret`: **TEMPLATE** com placeholders (`Jwt__Secret`, `ConnectionStrings__Default`, `AdminBootstrap__Password`, `Webhook__Token`, `NewRelic__LicenseKey`). No deploy real é criado pelo CI a partir de GitHub Secrets + outputs do Terraform. |
| `migration-job.yaml` | Job (`batch/v1`) | `oficina-migrate`: roda `dotnet Oficina.Api.dll migrate` (migração + bootstrap) e encerra. |
| `deployment.yaml` | Deployment (`apps/v1`) | `oficina-api`: 2 réplicas, probes em `/health:8080`, resources requests/limits, securityContext restritivo, rootfs read-only + `emptyDir` em `/tmp`, agente do New Relic ligado via `CORECLR_ENABLE_PROFILING=1`. |
| `service.yaml` | Service (`v1`) | `oficina-api`: `NodePort` `80 → 8080`. |
| `hpa.yaml` | HorizontalPodAutoscaler (`autoscaling/v2`) | `oficina-api`: 2–10 réplicas, CPU ~60% / memória ~70%. |

Todos os manifests namespaced usam `namespace: ${NAMESPACE}`, substituído pelo CI conforme
o ambiente (`oficina-hml` ou `oficina-prd`).

## Service NodePort (não LoadBalancer)

O `Service` é `NodePort`, não `LoadBalancer`. Quem cria o NLB interno é o Terraform do
repositório `oficina-infra-k8s`, que anexa o auto scaling group dos nós a um target group
apontando para a porta fixa do Service. O API Gateway alcança o cluster por VPC Link até
esse NLB. Um `Service type: LoadBalancer` criaria um segundo balanceador, público, fora do
controle do API Gateway — exatamente o que este desenho evita.

`nodePort` é único no **cluster inteiro**, não por namespace, então cada ambiente usa uma
porta diferente:

| Ambiente | Namespace | `NODE_PORT` |
|---|---|---|
| Produção | `oficina-prd` | `30080` |
| Homologação | `oficina-hml` | `30081` |

Quem define `${NODE_PORT}` por ambiente é o pipeline de CD.

## Ordem de aplicação

As imagens usam os placeholders `${ECR_REPOSITORY}` e `${IMAGE_TAG}`, e os manifests
namespaced usam `${NAMESPACE}` e (o Service) `${NODE_PORT}` — todos substituídos pelo CI
com `envsubst` (ex.: `envsubst < arquivo.yaml | kubectl apply -f -`).

```bash
kubectl apply -f namespace-prd.yaml   # ou namespace-hml.yaml, conforme o ambiente
kubectl apply -f configmap.yaml
kubectl apply -f secret.yaml          # no deploy real: kubectl create secret (ver secret.yaml)
kubectl apply -f migration-job.yaml   # migra + bootstrap
kubectl wait --for=condition=complete job/oficina-migrate -n ${NAMESPACE} --timeout=300s
kubectl apply -f deployment.yaml      # rollout só depois do Job concluir
kubectl apply -f service.yaml
kubectl apply -f hpa.yaml             # requer metrics-server (Terraform)
```

## Dependências (Terraform)

- **RDS PostgreSQL 16**: o endpoint alimenta `ConnectionStrings__Default` no Secret.
- **metrics-server**: obrigatório para o HPA (sem ele as métricas ficam `<unknown>`).
- **ECR**: repositório da imagem (`${ECR_REPOSITORY}`); a tag (`${IMAGE_TAG}`) é o SHA/versão do build do CI.
- **oficina-infra-k8s**: cria o NLB interno e o target group na porta `${NODE_PORT}` de cada ambiente.

## Migração vs. startup

Em Kubernetes os pods do Deployment **não** migram (`Bootstrap__ExecutarNoStartup=false`),
evitando corrida entre réplicas. O `Job` `oficina-migrate` migra + faz bootstrap uma única
vez, antes do rollout. No `docker-compose`/local o comportamento é o oposto por default
(sem a chave → migra no startup), por conveniência.

## New Relic

O agente do New Relic já vem instalado na imagem (`/usr/local/newrelic-dotnet-agent/`),
mas desligado por padrão (`CORECLR_ENABLE_PROFILING=0` na imagem). O `deployment.yaml`
liga o profiler (`CORECLR_ENABLE_PROFILING=1`) e lê a licença do Secret
`oficina-api-secret`, chave **`NewRelic__LicenseKey`**. Essa é a chave que o pipeline de
CD (que cria o Secret real) precisa preencher — nomes diferentes fazem o pod subir sem
licença, sem erro visível além do dashboard vazio.

Dois ajustes que o profiler **ligado** exige e que não apareciam com ele desligado:

- **Logs em `/tmp`.** Com `readOnlyRootFilesystem: true`, o destino padrão do agente
  (`$CORECLR_NEWRELIC_HOME/logs`) e do profiler nativo não é gravável. O Deployment
  define `NEW_RELIC_LOG_DIRECTORY=/tmp` e `NEWRELIC_PROFILER_LOG_DIRECTORY=/tmp`
  (o `emptyDir` montado em `/tmp`). Verificado rodando a imagem com
  `--read-only --tmpfs /tmp` e `CORECLR_ENABLE_PROFILING=1`: sem as variáveis, `/tmp`
  fica sem nenhum log do agente (diagnóstico silenciosamente perdido); com elas,
  aparecem `NewRelic.Profiler.<pid>.log` e `newrelic_agent_Oficina.Api.log`.
- **Memória.** Na mesma medição, em repouso: ~77Mi de RSS com o profiler desligado e
  ~215Mi com ele ligado. Por isso `limits.memory` é 512Mi (era 256Mi, dimensionado sem
  agente) e `requests.memory` é 256Mi.

- **Nome da aplicação.** `NEW_RELIC_APP_NAME` é `oficina-api;oficina-api-${AMBIENTE}`.
  O primeiro nome da lista é a entidade principal e é o que as consultas e os alertas
  da Fase 3 procuram (`appName = 'oficina-api'`); o segundo mantém hml e prd
  separáveis no APM. `${AMBIENTE}` (`prd`/`hml`) vem do `cd.yml`, não do `${NAMESPACE}`.
