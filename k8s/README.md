# Manifestos Kubernetes — Oficina API

Orquestração da API no EKS (Fase 2). O endpoint do RDS PostgreSQL, o `metrics-server`
e os segredos reais são provisionados pelo Terraform / CI (Plano 09).

## Arquivos

| Arquivo | Recurso (apiVersion) | O que cria |
|---|---|---|
| `namespace.yaml` | Namespace (`v1`) | Namespace `oficina`. |
| `configmap.yaml` | ConfigMap (`v1`) | `oficina-api-config`: config não-sensível (`ASPNETCORE_ENVIRONMENT`, `Jwt__Issuer`, `Jwt__Audience`, `Bootstrap__ExecutarNoStartup=false`). |
| `secret.yaml` | Secret (`v1`) | `oficina-api-secret`: **TEMPLATE** com placeholders (`Jwt__Secret`, `ConnectionStrings__Default`, `AdminBootstrap__Password`, `Webhook__Token`). No deploy real é criado pelo CI a partir de GitHub Secrets + outputs do Terraform. |
| `migration-job.yaml` | Job (`batch/v1`) | `oficina-migrate`: roda `dotnet Oficina.Api.dll migrate` (migração + bootstrap) e encerra. |
| `deployment.yaml` | Deployment (`apps/v1`) | `oficina-api`: 2 réplicas, probes em `/health:8080`, resources requests/limits, securityContext restritivo, rootfs read-only + `emptyDir` em `/tmp`. |
| `service.yaml` | Service (`v1`) | `oficina-api`: `LoadBalancer` `80 → 8080`. |
| `hpa.yaml` | HorizontalPodAutoscaler (`autoscaling/v2`) | `oficina-api`: 2–10 réplicas, CPU ~60% / memória ~70%. |

## Ordem de aplicação

As imagens usam os placeholders `${ECR_REPOSITORY}` e `${IMAGE_TAG}`, substituídos pelo CI
(ex.: `envsubst < arquivo.yaml | kubectl apply -f -`).

```bash
kubectl apply -f namespace.yaml
kubectl apply -f configmap.yaml
kubectl apply -f secret.yaml          # no deploy real: kubectl create secret (ver secret.yaml)
kubectl apply -f migration-job.yaml   # migra + bootstrap
kubectl wait --for=condition=complete job/oficina-migrate -n oficina --timeout=300s
kubectl apply -f deployment.yaml      # rollout só depois do Job concluir
kubectl apply -f service.yaml
kubectl apply -f hpa.yaml             # requer metrics-server (Terraform, Plano 09)
```

## Dependências (Terraform — Plano 09)

- **RDS PostgreSQL 16**: o endpoint alimenta `ConnectionStrings__Default` no Secret.
- **metrics-server**: obrigatório para o HPA (sem ele as métricas ficam `<unknown>`).
- **ECR**: repositório da imagem (`${ECR_REPOSITORY}`); a tag (`${IMAGE_TAG}`) é o SHA/versão do build do CI.

## Migração vs. startup

Em Kubernetes os pods do Deployment **não** migram (`Bootstrap__ExecutarNoStartup=false`),
evitando corrida entre réplicas. O `Job` `oficina-migrate` migra + faz bootstrap uma única
vez, antes do rollout. No `docker-compose`/local o comportamento é o oposto por default
(sem a chave → migra no startup), por conveniência.

## Alternativa (opcional): Ingress/ALB

Em vez de `Service type: LoadBalancer`, pode-se usar `type: ClusterIP` + um `Ingress`
(AWS Load Balancer Controller / ALB) para roteamento L7, TLS e path-based routing.
Não é obrigatório para o MVP.
