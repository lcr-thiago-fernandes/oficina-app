# Infraestrutura (Terraform / AWS) — Fase 2

Provisiona o alvo dos manifestos do Plano 08 (`k8s/`): cluster **EKS**, **RDS PostgreSQL 16**
privado, repositorio **ECR**, **OIDC do GitHub Actions** (CI do Plano 10) e o **metrics-server**
(exigido pelo HPA). Estado remoto em **S3 + DynamoDB**. Custo controlado — **destrua apos a demo**.

## Recursos criados

| Arquivo | Recurso(s) | O que cria |
|---|---|---|
| `versions.tf` | terraform / required_providers | Pins: terraform >= 1.5, aws ~> 5.60, kubernetes ~> 2.31, helm ~> 2.14, tls ~> 4.0. |
| `backend.tf` | backend S3 + DynamoDB | State em S3 (`fase2/terraform.tfstate`) com lock DynamoDB. |
| `providers.tf` | aws / kubernetes / helm / data eks | Providers; kubernetes/helm autenticam via outputs do EKS. |
| `vpc.tf` | `terraform-aws-modules/vpc/aws` (~> 5.13) | VPC 2 AZs, subnets publicas+privadas, **single NAT**, tags de ELB. |
| `eks.tf` | `terraform-aws-modules/eks/aws` (~> 20.24) | Cluster EKS + 1 node group (`t3.medium`, 2/1/3), addons, **access entry** admin p/ CI. |
| `rds.tf` | `aws_db_instance` + SG + subnet group | PostgreSQL 16 `db.t3.micro` 20GB gp3, single-AZ, privado, 5432 so do SG dos nos. |
| `ecr.tf` | `aws_ecr_repository` + lifecycle | Repo `oficina-api`, scan on push, mantem ultimas 10 imagens. |
| `iam-github-oidc.tf` | OIDC provider + role + policies | Trust restrita a `repo:lcr-thiago-fernandes/fiap_15SOAT_fase1:*`, ECR push/pull + EKS describe. |
| `helm.tf` | `helm_release` metrics-server | metrics-server no `kube-system` (HPA do Plano 08). |
| `outputs.tf` | outputs | `cluster_name`, `cluster_endpoint`, `ecr_repository_url`, `rds_endpoint`, `github_actions_role_arn`, … |

## Pre-requisitos

- AWS CLI configurada (`aws configure`) com permissoes de admin na conta.
- Terraform >= 1.5.
- Custo: recursos pagos (EKS/NAT/RDS/ELB). **Destrua apos a demo** (ver abaixo).

## 1) Bootstrap do backend (UMA vez, ANTES do `terraform init`)

O bucket S3 e a tabela DynamoDB do `backend.tf` precisam existir antes do `init`
(chicken-and-egg). Crie-os com a AWS CLI. Os nomes DEVEM bater com o `backend.tf`.

```bash
export AWS_REGION=us-east-1
export TF_STATE_BUCKET=oficina-tfstate-fiap-15soat   # nome S3 e GLOBALMENTE unico; ajuste se preciso
export TF_LOCK_TABLE=oficina-tfstate-lock

# Bucket de state (us-east-1 NAO aceita LocationConstraint):
aws s3api create-bucket --bucket "$TF_STATE_BUCKET" --region "$AWS_REGION"
# (para outra regiao, use: --create-bucket-configuration LocationConstraint=$AWS_REGION)

# Versionamento + criptografia + bloqueio de acesso publico no bucket:
aws s3api put-bucket-versioning --bucket "$TF_STATE_BUCKET" \
  --versioning-configuration Status=Enabled
aws s3api put-bucket-encryption --bucket "$TF_STATE_BUCKET" \
  --server-side-encryption-configuration '{"Rules":[{"ApplyServerSideEncryptionByDefault":{"SSEAlgorithm":"AES256"}}]}'
aws s3api put-public-access-block --bucket "$TF_STATE_BUCKET" \
  --public-access-block-configuration BlockPublicAcls=true,IgnorePublicAcls=true,BlockPublicPolicy=true,RestrictPublicBuckets=true

# Tabela de lock (chave LockID):
aws dynamodb create-table \
  --table-name "$TF_LOCK_TABLE" \
  --attribute-definitions AttributeName=LockID,AttributeType=S \
  --key-schema AttributeName=LockID,KeyType=HASH \
  --billing-mode PAY_PER_REQUEST \
  --region "$AWS_REGION"
```

## 2) Provisionar

```bash
cd infra

# Senha do RDS via variavel de ambiente (preferivel a terraform.tfvars):
export TF_VAR_db_password='use-uma-senha-forte-aqui'

terraform init      # conecta ao backend S3 (ja bootstrapado)
terraform plan      # revise o plano
terraform apply     # cria a infraestrutura (~15-20 min pelo EKS)
```

## 3) Acessar o cluster (kubeconfig)

```bash
aws eks update-kubeconfig --region us-east-1 --name $(terraform output -raw cluster_name)
kubectl get nodes
```

Depois disso, os manifestos do Plano 08 (`k8s/`) podem ser aplicados (ver `k8s/README.md`).

## 4) Destruir (APOS a demo — evita custo)

```bash
# Remova primeiro o Service LoadBalancer do K8s (senao o ELB orfao trava o destroy da VPC):
kubectl delete -f ../k8s/service.yaml --ignore-not-found

terraform destroy
```

> O bucket S3 e a tabela DynamoDB do backend NAO sao gerenciados pelo Terraform (bootstrap manual);
> remova-os a mao se quiser zerar tudo: `aws s3 rb s3://$TF_STATE_BUCKET --force` e
> `aws dynamodb delete-table --table-name $TF_LOCK_TABLE`.

## Custo (estimativa `us-east-1`, ordem de grandeza)

| Item | Aprox. |
|---|---|
| EKS control plane | ~US$0,10/h (~US$73/mes) |
| 2x `t3.medium` (on-demand) | ~US$60/mes |
| NAT Gateway (single) | ~US$32/mes + trafego |
| RDS `db.t3.micro` single-AZ + 20GB gp3 | ~US$15/mes |
| ELB do Service LoadBalancer (Plano 08) | ~US$18/mes |

**Total na ordem de ~US$180/mes se ficar ligado.** Para a demo, suba, apresente e rode
`terraform destroy`. Reduza mais trocando `node_instance_type` para `t3.small`.

## Como os outputs alimentam CI (Plano 10) e K8s (Plano 08)

- `github_actions_role_arn` -> secret `AWS_ROLE_ARN` no GitHub (o workflow assume via OIDC, sem chave estatica).
- `ecr_repository_url` -> `${ECR_REPOSITORY}` nos manifestos do Plano 08 (o CI faz `envsubst`); a tag e o SHA do build.
- `rds_endpoint` (+ `rds_port`, `db_name`, `db_username`, `db_password`) -> `ConnectionStrings__Default`
  do Secret `oficina-api-secret` (`k8s/secret.yaml`), criado pelo CI via `kubectl create secret`.
- `cluster_name` -> `aws eks update-kubeconfig` no job de deploy do CI.
- A **access entry** do EKS concede admin do cluster a `github_actions_role_arn` -> o CI faz `kubectl apply`.
