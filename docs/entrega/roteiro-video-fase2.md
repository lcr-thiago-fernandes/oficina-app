# Roteiro do vídeo — Tech Challenge Fase 2 (≤ 15 min)

> Objetivo: demonstrar **deploy da aplicação**, **execução do CI/CD**, **consumo das APIs**
> e **escalabilidade automática (HPA)**. Grave em ≤15 min (YouTube/Vimeo).

## 0. Abertura (0:00–0:45)
- Apresentar-se e resumir a solução (oficina mecânica, .NET 8 + PostgreSQL, Clean Architecture).
- Mostrar o repositório e o README (os 3 diagramas: camadas, infra AWS, fluxo CI/CD).

## 1. Infraestrutura provisionada (0:45–3:00)
- Mostrar `infra/` (Terraform) e explicar em uma frase cada recurso: VPC, EKS, RDS, ECR, OIDC, metrics-server.
- (Pré-provisionado antes da gravação, pois `terraform apply` leva ~15–20 min.) Mostrar:
  ```bash
  terraform -chdir=infra output      # cluster_name, ecr_repository_url, rds_endpoint, github_actions_role_arn
  aws eks update-kubeconfig --region us-east-1 --name $(terraform -chdir=infra output -raw cluster_name)
  kubectl get nodes
  ```

## 2. Pipeline CI/CD (3:00–6:00)
- Abrir a aba **Actions** no GitHub. Mostrar o **CI** verde (build + testes ≥80% + CodeQL + docker build).
- Disparar/mostrar o **CD** (push em `main` ou `workflow_dispatch`). Percorrer os steps:
  OIDC → build/push no ECR (tag = SHA) → `kubectl create secret` → **Job de migração** + `kubectl wait`
  → apply do Deployment/Service/HPA → `kubectl rollout status`.
- Destacar: sem chave estática (OIDC) e migração isolada em Job (sem corrida entre réplicas).

## 3. Deploy da aplicação no cluster (6:00–8:00)
```bash
kubectl get pods,svc,hpa -n oficina
kubectl logs job/oficina-migrate -n oficina   # migração + bootstrap do admin
EXTERNAL_IP=$(kubectl get svc oficina-api -n oficina -o jsonpath='{.status.loadBalancer.ingress[0].hostname}')
curl -fsS http://$EXTERNAL_IP/health
```
- Abrir o Swagger (ou usar os `.http`) apontando para o `EXTERNAL_IP`.

## 4. Consumo das APIs (8:00–11:00)
Usar `http/` (ou Swagger), na ordem:
1. **Login** (`auth.http`) — copiar o `accessToken`.
2. **Cadastros base** — serviço (`servicos.http`) e peça + entrada de estoque (`pecas.http`).
3. **Abrir OS consolidada** (`ordens-servico.http`) — mostrar `numero` (BIGSERIAL) e itens.
4. **Fluxo de estados** — diagnóstico → enviar orçamento.
5. **Aprovação via webhook** (`webhook-aprovacao.http`) — `X-Webhook-Token`, `{ "decisao": "aprovado" }`;
   mostrar 401 com token errado.
6. **Consulta pública** (`consulta.http`) — GET pelo número + documento; mostrar 404 anti-enumeração.
7. **Iniciar execução** — mostrar a baixa transacional de estoque; finalizar; entregar.
8. **Métricas admin** — tempo médio de execução.

## 5. Escalabilidade automática — HPA (11:00–14:00)
- Mostrar o estado inicial: `kubectl get hpa -n oficina` (2 réplicas, CPU baixa).
- Gerar carga contra o endpoint (escolher uma opção):
  ```bash
  # Opção A — hey
  hey -z 90s -c 100 http://$EXTERNAL_IP/health

  # Opção B — k6
  k6 run - <<'EOF'
  import http from 'k6/http';
  import { sleep } from 'k6';
  export const options = { vus: 100, duration: '90s' };
  export default function () { http.get(`http://${__ENV.HOST}/health`); sleep(0.1); }
  EOF
  # HOST=$EXTERNAL_IP k6 run script.js

  # Opção C — sem ferramenta: abrir muitas OS em loop com curl + token
  ```
- Acompanhar o autoscaling ao vivo:
  ```bash
  kubectl get hpa -n oficina -w        # utilização de CPU sobe; réplicas 2 -> ... -> até 10
  kubectl get pods -n oficina -w       # novos pods "Running"
  ```
- Parar a carga e mostrar o **scale-down** (pode levar alguns minutos pela janela de estabilização).

## 6. Encerramento (14:00–15:00)
- Recapitular: Clean Architecture (5 projetos), EKS+RDS via Terraform, CI/CD OIDC, HPA, observabilidade `/metrics`.
- Lembrete: `terraform destroy` após a demo (custo). Agradecer.

## Checklist de gravação
- [ ] Infra já provisionada e `kubectl` conectado ao EKS antes de gravar.
- [ ] Segredos preenchidos (GitHub Secrets/Variables) e admin com senha conhecida.
- [ ] `EXTERNAL_IP` do Service resolvendo.
- [ ] Ferramenta de carga instalada (`hey` ou `k6`).
- [ ] Duração final ≤ 15 min.
