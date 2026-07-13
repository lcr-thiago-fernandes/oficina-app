# Guia de deploy e entrega — Fase 2 (passo a passo do que depende de você)

Este runbook cobre **apenas os passos que exigem a sua conta AWS/GitHub e a sua ação** (o código, os manifestos, a IaC e as pipelines já estão prontos no repositório). Cada passo tem **o que fazer**, **por quê** e a **justificativa amarrada ao enunciado da Fase 2**.

> **Objetivo da fase (enunciado):** *"Evoluir a aplicação da Fase 1 para garantir qualidade, resiliência e escalabilidade, incorporando práticas modernas de infraestrutura e automação."* Os passos abaixo materializam exatamente isso.

## Mapa: requisito do enunciado → onde é atendido

| Requisito obrigatório (enunciado) | Atendido por | Passo(s) aqui |
|---|---|---|
| Conteinerização (Dockerfile + docker-compose) | `docker/` (já no repo) | 0 (validação local, opcional) |
| Orquestração K8s (Deployments, Services, ConfigMaps/Secrets, **HPA**) | `k8s/` | 5, 6, 7 |
| IaC **Terraform** (cluster + **banco de dados** + documentar) | `infra/` | 1, 2 |
| CI/CD (build → testes → imagem → deploy no cluster → deploy do banco → aplicar manifestos) | `.github/workflows/{ci,cd,infra}.yml` | 4, 5 |
| Escalabilidade dinâmica (picos de OS) | HPA + `metrics-server` | 7 |
| Entregáveis (repo, README+arquitetura, collection, **vídeo**, **PDF**) | `README.md`, `docs/entrega/` | 8, 9 |

---

## 0. Pré-requisitos (sua máquina/conta)

**O quê:** ter uma **conta AWS** com permissões de admin e as CLIs instaladas:
- AWS CLI v2 (`aws configure` com suas credenciais),
- Terraform >= 1.5,
- `kubectl`,
- (opcional) `gh` para o GitHub.

**Por quê:** todo o provisionamento e o deploy assumem essas ferramentas. Na sua máquina de desenvolvimento atual elas não estavam instaladas — por isso os testes de integração e o `terraform/kubectl` não rodaram localmente e ficaram para o CI/deploy.

**Justificativa (enunciado):** o desafio pede infraestrutura provisionada e automatizada em nuvem — a conta AWS é o alvo de todo o `infra/` e `k8s/`.

---

## 1. Bootstrap do backend do Terraform (uma única vez)

**O quê:** criar o bucket S3 + tabela DynamoDB que guardam o *state* do Terraform (o `infra/backend.tf` referencia nomes fixos). Rode uma vez:

```bash
export AWS_REGION=us-east-1
export TF_STATE_BUCKET=oficina-tfstate-fiap-15soat   # nome S3 é GLOBALMENTE único; ajuste o sufixo se colidir (e atualize infra/backend.tf)
export TF_LOCK_TABLE=oficina-tfstate-lock

aws s3api create-bucket --bucket "$TF_STATE_BUCKET" --region "$AWS_REGION"
aws s3api put-bucket-versioning --bucket "$TF_STATE_BUCKET" --versioning-configuration Status=Enabled
aws s3api put-bucket-encryption --bucket "$TF_STATE_BUCKET" \
  --server-side-encryption-configuration '{"Rules":[{"ApplyServerSideEncryptionByDefault":{"SSEAlgorithm":"AES256"}}]}'
aws s3api put-public-access-block --bucket "$TF_STATE_BUCKET" \
  --public-access-block-configuration BlockPublicAcls=true,IgnorePublicAcls=true,BlockPublicPolicy=true,RestrictPublicBuckets=true
aws dynamodb create-table --table-name "$TF_LOCK_TABLE" \
  --attribute-definitions AttributeName=LockID,AttributeType=S \
  --key-schema AttributeName=LockID,KeyType=HASH \
  --billing-mode PAY_PER_REQUEST --region "$AWS_REGION"
```

**Por quê:** o Terraform guarda o state remoto (S3) com trava de concorrência (DynamoDB). É *chicken-and-egg*: o backend precisa existir **antes** do `terraform init`. O bucket é criptografado, versionado e sem acesso público.

**Justificativa (enunciado):** o módulo de Terraform do curso ensina **state remoto (Providers e Backend)**; usar S3+DynamoDB é a prática esperada de IaC — e o state guarda o inventário do "provisionamento automatizado" que o desafio pede.

---

## 2. Provisionar a infraestrutura (Terraform → EKS + RDS + ECR + OIDC + metrics-server)

**O quê:**
```bash
cd infra
export TF_VAR_db_password='use-uma-senha-forte-aqui'   # senha do RDS (sensível; não commitar)
terraform init      # conecta ao backend S3
terraform plan      # revise o que será criado
terraform apply     # ~15-20 min (o EKS demora)
```
Ao final, guarde os outputs (usados nos passos 3 e 4):
```bash
terraform output cluster_name            # ex.: oficina-eks
terraform output ecr_repository_url
terraform output rds_endpoint
terraform output github_actions_role_arn
```

**Por quê:** um único `terraform apply` cria a **VPC** (2 AZs, NAT único), o **cluster EKS** (node group pequeno), o **RDS PostgreSQL 16 privado** (o "banco de dados" do enunciado), o repositório **ECR** (registry da imagem), o **OIDC do GitHub Actions** (deploy sem chave estática) e o **metrics-server** (necessário para o HPA escalar).

**Justificativa (enunciado):** atende diretamente *"Criar scripts em Terraform para provisionamento do cluster Kubernetes e Banco de Dados; documentar quais recursos são criados"* — a documentação está em `infra/README.md`, e o RDS provisionado é o "deploy do banco de dados" exigido na pipeline. O tema de **resiliência/escalabilidade** vem do EKS gerenciado + multi-AZ + HPA.

> **Custo:** ~US$180/mês se ficar ligado (EKS + nós + NAT + RDS + ELB). **Suba só para a demo e destrua depois (passo 10).**

---

## 3. Apontar o `kubectl` para o cluster

**O quê:**
```bash
aws eks update-kubeconfig --region us-east-1 --name $(terraform output -raw cluster_name)
kubectl get nodes    # deve listar os nós do node group
```

**Por quê:** o `kubectl` precisa de um kubeconfig apontando para o EKS para você inspecionar/validar o cluster (passos 6 e 7). O CI faz isso sozinho no deploy; aqui é para você acompanhar/demonstrar.

**Justificativa (enunciado):** parte de "orquestração com Kubernetes" — operar e observar o cluster (`kubectl get pods,hpa,svc`).

---

## 4. Configurar os GitHub Secrets/Variables (habilita o CI/CD)

**O quê:** em *Settings → Secrets and variables → Actions* do repositório, preencha com os valores do passo 2:

**Secrets:**
| Secret | Valor |
|---|---|
| `AWS_ROLE_ARN` | output `github_actions_role_arn` |
| `DB_USER` | usuário do RDS (var `db_username`, default `oficina`) |
| `DB_PASSWORD` | a senha que você usou em `TF_VAR_db_password` |
| `RDS_ENDPOINT` | output `rds_endpoint` |
| `JWT_SECRET` | string ≥ 64 chars (HS256) |
| `ADMIN_BOOTSTRAP_PASSWORD` | senha inicial do admin |
| `WEBHOOK_TOKEN` | token forte do webhook de aprovação |
| `AWS_TERRAFORM_ROLE_ARN` | (opcional) role de infra, se for usar o `infra.yml` |

**Variables:**
| Variable | Valor |
|---|---|
| `AWS_REGION` | `us-east-1` |
| `EKS_CLUSTER_NAME` | output `cluster_name` (`oficina-eks`) |
| `ECR_REPOSITORY` | output `ecr_repository_url` (URL completa) |
| `JWT_ISSUER` | `oficina-api` |
| `JWT_AUDIENCE` | `oficina-clients` |

**Por quê:** o `cd.yml` assume o role AWS via **OIDC** (sem chave estática), monta o Secret do Kubernetes a partir desses valores e substitui a imagem do ECR nos manifestos. Sem eles, o CD falha no primeiro passo (foi o que aconteceu no push inicial — é o esperado até aqui).

**Justificativa (enunciado):** *"Pipeline de CI/CD configurada"* — os segredos são o que liga a pipeline à sua infraestrutura de forma segura (o desafio cita explicitamente **Secrets para tokens de serviços externos**, caso do `WEBHOOK_TOKEN`).

---

## 5. Executar o deploy (CI/CD) no cluster

**O quê:** o `cd.yml` dispara em **push na `main`** (já houve um push; ele falhou por falta dos secrets do passo 4). Depois do passo 4, rode o deploy de novo:
- **Actions → CD → o run que falhou → “Re-run all jobs”**, ou
- faça um commit trivial e `git push origin main`.

Acompanhe: o job faz `build → push da imagem p/ ECR → Job de migração no RDS (kubectl wait) → apply dos manifestos → rollout`.

**Por quê:** o `cd.yml` automatiza tudo o que o enunciado lista para a pipeline. O **Job de migração** (`oficina-migrate`) aplica o schema no RDS antes do rollout (por isso os pods do Deployment não migram — evita corrida entre réplicas).

**Justificativa (enunciado):** cobre item a item o requisito de CI/CD — *"Build da aplicação; Execução dos testes automatizados (no `ci.yml`); Build da imagem Docker; Deploy no cluster Kubernetes; **Deploy do banco de dados** (RDS via Terraform + Job de migração do schema); Aplicação dos manifestos YAML no cluster"*.

---

## 6. Validar a aplicação no cluster

**O quê:**
```bash
kubectl get pods,svc,hpa -n oficina
# pegue o hostname do LoadBalancer:
export API=$(kubectl get svc oficina-api -n oficina -o jsonpath='{.status.loadBalancer.ingress[0].hostname}')
curl -fsS "http://$API/health"     # {"status":"ok"}
curl -fsS "http://$API/metrics" | head    # métricas Prometheus
# exercite as APIs: login, abertura de OS, consulta, webhook — use os arquivos http/ ou o Swagger (/swagger em Development)
```

**Por quê:** confirma que o Deployment subiu (2 réplicas), o Service publicou um ELB e a app responde. É o material do vídeo (consumo das APIs).

**Justificativa (enunciado):** demonstra a solução em execução e o **consumo das APIs** exigido no vídeo.

---

## 7. Demonstrar a escalabilidade automática (HPA)

**O quê:** gere carga e observe o HPA escalar as réplicas:
```bash
# terminal 1 — observar o HPA e os pods:
kubectl get hpa -n oficina -w
# terminal 2 — gerar carga (escolha uma opção):
hey -z 3m -c 100 "http://$API/health"                 # se tiver 'hey'
# ou k6:  k6 run --vus 100 --duration 3m - <<'EOF'
# import http from 'k6/http'; export default () => http.get(`http://${__ENV.API}/health`);
# EOF
# ou muitas OS: dispare em loop o POST /api/v1/ordens-servico (payload em http/ordens-servico.http)
```
Você deve ver o `TARGETS` de CPU subir e o `REPLICAS` ir de 2 em direção a 10.

**Por quê:** o HPA (`autoscaling/v2`, 2→10, CPU ~60%/mem ~70%) usa o `metrics-server` (provisionado no passo 2) para escalar sob carga.

**Justificativa (enunciado):** atende diretamente *"Preparar a aplicação para suportar grandes volumes de ordens de serviço em horários de pico, com escalabilidade dinâmica"* e o item do vídeo *"Escalabilidade automática (pode simular aumento de carga ou múltiplas ordens de serviço)"*.

---

## 8. Gravar o vídeo demonstrativo (≤ 15 min)

**O quê:** grave e publique (YouTube/Vimeo, público ou não listado) cobrindo, nesta ordem (roteiro pronto em [`roteiro-video-fase2.md`](roteiro-video-fase2.md)):
1. **Deploy da aplicação** (passos 5-6);
2. **Execução do CI/CD** (a run do GitHub Actions);
3. **Consumo das APIs** (login, abrir OS, consultar status, webhook de aprovação);
4. **Escalabilidade automática** (passo 7 — HPA escalando).

Cole o link no README e no PDF de entrega.

**Justificativa (enunciado):** o vídeo ≤15min é **entregável obrigatório** e o enunciado lista exatamente esses 4 pontos.

---

## 9. Entrega no portal + PDF

**O quê:**
- Garanta que o repositório privado está **compartilhado com o usuário `soat-architecture`**.
- Gere o **PDF** a partir de [`__ENTREGA-fase2.md`](__ENTREGA-fase2.md) (contém equipe/Discord, link do repo, desenho da arquitetura — via diagramas do README — e o link do vídeo). Ferramentas: VS Code “Markdown PDF”, Pandoc, etc.
- Suba o PDF no portal do aluno.

**Justificativa (enunciado):** *"Entrega no portal do aluno: PDF contendo o link do repositório github compartilhado com o usuário `soat-architecture`, desenho da arquitetura e link do vídeo."*

---

## 10. Destruir a infraestrutura (após a demo — controle de custo)

**O quê:**
```bash
kubectl delete -f k8s/service.yaml --ignore-not-found   # remove o ELB ANTES (senão o ELB órfão trava o destroy da VPC)
cd infra && terraform destroy
```

**Por quê:** EKS/NAT/RDS/ELB são pagos por hora. A decisão de arquitetura (ADR-010) é **provisionar sob demanda e destruir após a demo**.

**Justificativa (enunciado):** o desafio pede "reduzir riscos operacionais" e infra escalável — mas para um MVP acadêmico, subir só na demonstração é a escolha responsável de custo (documentada em `infra/README.md` e no ADR-010).

---

## 11. (Opcional) Renomear a pasta local

Com a sessão/editor fechados:
```bash
mv "C:/Users/thiago/source/repos/Fiap/fiap_15SOAT_fase1" "C:/Users/thiago/source/repos/Fiap/fiap_15SOAT_fase2"
```
Cosmético (o repositório no GitHub já é `fiap_15SOAT_fase2` e o `git remote` local já aponta para ele).

---

### Ordem recomendada
**0 → 1 → 2 → 3 → 4 → 5 → 6 → 7 → 8 → 9 → 10** (e 11 quando quiser). Os passos 1-3 são "uma vez"; 4 é "uma vez" (a menos que rotacione segredos); 5-7 você repete/demonstra; 10 é a limpeza final.
