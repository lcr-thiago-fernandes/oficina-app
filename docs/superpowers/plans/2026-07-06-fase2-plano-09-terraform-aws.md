# Fase 2 — Plano 09: Infraestrutura como Código (Terraform / AWS)

> **For agentic workers:** REQUIRED SUB-SKILL: `superpowers:subagent-driven-development`. Steps usam checkbox (`- [ ]`). É **UMA Task** (um único commit). Este plano SÓ **escreve arquivos** em `infra/` — **nada de C# muda**. `terraform` e a AWS **não estão disponíveis localmente**: a validação real é a pessoa rodar `terraform init/plan/apply` na conta AWS dela e `terraform destroy` após a demo. O gate LOCAL desta task é: arquivos criados + **verificações estáticas** (greps/estrutura do Step de verificação) + `dotnet build Oficina.sln` **inalterado (0 erros)**.

**Goal:** Provisionar, com **custo controlado**, o alvo dos manifestos do Plano 08 (`k8s/`): um cluster **EKS** (VPC 2 AZs, single NAT, 1 managed node group pequeno), **RDS PostgreSQL 16** privado, repositório **ECR** `oficina-api`, **OIDC do GitHub Actions** (sem chaves estáticas, com **access entry** de admin no EKS para o CI do Plano 10) e o **metrics-server** (via Helm) exigido pelo HPA. Estado remoto em **S3 + DynamoDB** (lock).

**Architecture:** Terraform HCL idiomático em `infra/`, um arquivo por preocupação. Reuso de **módulos oficiais versionados** (`terraform-aws-modules/vpc/aws`, `terraform-aws-modules/eks/aws`) — alinhado à Aula 6 (Módulos) e Aula 8 (Criação Infra com AWS) do curso. Providers `kubernetes`/`helm` configurados a partir dos **outputs do EKS** (endpoint + CA + token via `data aws_eks_cluster`/`aws_eks_cluster_auth`) — Aula 2 (Providers e Backend). Os **outputs** (`ecr_repository_url`, `cluster_name`, `rds_endpoint`, `github_actions_role_arn`, …) alimentam o CI (Plano 10) e a criação do Secret do K8s (Plano 08).

**Tech Stack:** Terraform >= 1.5, provider `hashicorp/aws ~> 5.x`, `hashicorp/kubernetes ~> 2.x`, `hashicorp/helm ~> 2.x`, `hashicorp/tls ~> 4.0`. AWS: VPC, EKS 1.30, EC2 managed node group (`t3.medium`), RDS PostgreSQL 16 (`db.t3.micro`, gp3, single-AZ), ECR, IAM OIDC. Backend: S3 (state) + DynamoDB (lock). Helm chart `metrics-server` (repo kubernetes-sigs).

## Global Constraints

- **Idioma pt-BR** em comentários, README e mensagem de commit. HCL idiomático (nomes de recurso em `snake_case`; um arquivo por assunto; nada de hardcoding de valores personalizáveis → tudo via `variable`).
- **NENHUM segredo real no repositório.** Senhas/segredos SÓ via `variable` sensível (`db_password`) — sem `default`, sem `access_key`/`secret_key`, sem chave estática em lugar nenhum. `terraform.tfvars.example` traz apenas **placeholders**. O `terraform.tfvars` real (com senha) é ignorado pelo `infra/.gitignore`.
- **Custo minimizado:** `single_nat_gateway = true`, RDS **single-AZ** `db.t3.micro`, node group pequeno (`desired=2 min=1 max=3`), `skip_final_snapshot = true`. README instrui **`terraform destroy` após a demo**.
- **terraform/AWS indisponíveis localmente** → validação estática + apply da pessoa. **`dotnet build Oficina.sln` deve seguir 0 erros** (nada de C# muda; só arquivos novos em `infra/`).
- **Backend é chicken-and-egg:** o bucket S3 e a tabela DynamoDB precisam existir **antes** do `terraform init`. O README documenta o **bootstrap** (comandos `aws s3api create-bucket` / `aws dynamodb create-table` exatos), executado **uma única vez** via AWS CLI.
- **Branch:** `fase-2` (sem merge/push). **Bash tool = Git Bash.** **UM commit** (uma Task), mensagem pt-BR terminando com:
  `Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>`

## Estrutura de arquivos

```
infra/versions.tf              NOVO  (terraform required_version + required_providers pinados)
infra/backend.tf               NOVO  (backend "s3" + dynamodb_table; valores literais — bootstrap manual)
infra/providers.tf             NOVO  (provider aws/kubernetes/helm/tls; data eks_cluster + eks_cluster_auth)
infra/variables.tf             NOVO  (region, project, versões, node, db_*, github_repo, ecr_*)
infra/locals.tf                NOVO  (data aws_availability_zones; locals name/cluster_name/azs/tags)
infra/vpc.tf                   NOVO  (module terraform-aws-modules/vpc/aws — 2 AZs, single NAT, tags EKS)
infra/eks.tf                   NOVO  (module terraform-aws-modules/eks/aws — node group + access entry CI)
infra/rds.tf                   NOVO  (db subnet group + SG 5432 do EKS + aws_db_instance postgres 16)
infra/ecr.tf                   NOVO  (aws_ecr_repository oficina-api + lifecycle policy)
infra/iam-github-oidc.tf       NOVO  (OIDC provider + role trust restrita ao repo + policies ECR/EKS)
infra/helm.tf                  NOVO  (helm_release metrics-server no kube-system)
infra/outputs.tf               NOVO  (cluster_name/endpoint, ecr_repository_url, rds_endpoint, role arn, …)
infra/terraform.tfvars.example NOVO  (placeholders — SEM segredo real)
infra/.gitignore               NOVO  (ignora *.tfstate, .terraform/, *.tfvars exceto *.example)
infra/README.md                NOVO  (recursos, bootstrap, init/plan/apply/destroy, kubeconfig, custo, CI)
```

15 arquivos, todos novos, todos sob `infra/`. Nenhum arquivo existente é modificado.

---

## Task 1: Provisionamento AWS via Terraform (`infra/` — validação estática)

**Files:** criar os 15 arquivos listados acima.

> **Sem terraform/AWS local.** Cada arquivo é validado por inspeção + `grep` (Step 14) e o gate `dotnet build` (Step 15). `terraform init/plan/apply` roda na conta da pessoa (README). Os placeholders `${...}` do Plano 08 (`ECR_REPOSITORY`, `IMAGE_TAG`) são preenchidos pelo CI (Plano 10) a partir dos outputs deste Terraform.

- [ ] **Step 1: `infra/versions.tf`** — versão do Terraform + providers pinados (Aula 2: fixar versões).

  ```hcl
  # Versões pinadas do Terraform e dos providers (Aula 2 — boas práticas: fixar versão).
  terraform {
    required_version = ">= 1.5"

    required_providers {
      aws = {
        source  = "hashicorp/aws"
        version = "~> 5.60"
      }
      kubernetes = {
        source  = "hashicorp/kubernetes"
        version = "~> 2.31"
      }
      helm = {
        source  = "hashicorp/helm"
        version = "~> 2.14"
      }
      tls = {
        source  = "hashicorp/tls"
        version = "~> 4.0"
      }
    }
  }
  ```

- [ ] **Step 2: `infra/backend.tf`** — estado remoto S3 + lock DynamoDB (Aula 2). **Valores literais** (o bloco `backend` NÃO aceita variáveis); precisam bater com o bootstrap do README.

  ```hcl
  # Estado remoto no S3 com bloqueio via DynamoDB (Aula 2 — Providers e Backend).
  #
  # ATENCAO (chicken-and-egg): o bucket e a tabela abaixo precisam EXISTIR antes do
  # primeiro `terraform init`. Crie-os UMA vez com a AWS CLI (ver infra/README.md -> Bootstrap).
  #
  # O nome do bucket S3 e GLOBALMENTE unico: ajuste o sufixo se ja estiver em uso
  # (e replique o mesmo nome no comando de bootstrap do README).
  terraform {
    backend "s3" {
      bucket         = "oficina-tfstate-fiap-15soat"
      key            = "fase2/terraform.tfstate"
      region         = "us-east-1"
      dynamodb_table = "oficina-tfstate-lock"
      encrypt        = true
    }
  }
  ```

  > Se você alterar `var.region` para uma região diferente de `us-east-1`, ajuste também `region` aqui e no bootstrap (o bucket de state vive na região do backend, independente da região dos recursos).

- [ ] **Step 3: `infra/providers.tf`** — provider AWS (region via var, `default_tags`) + `kubernetes`/`helm` a partir dos outputs do EKS (Aula 2: provider `aws` e provider de Kubernetes).

  ```hcl
  # Provider AWS: regiao via variavel (default us-east-1). Sem credenciais no codigo —
  # autenticacao vem do ambiente (aws configure / variaveis de ambiente / OIDC no CI).
  provider "aws" {
    region = var.region

    default_tags {
      tags = local.tags
    }
  }

  # Autenticacao no cluster para os providers kubernetes/helm: endpoint + CA + token
  # derivados dos outputs do EKS (via data sources). O `name` depende de module.eks,
  # entao a leitura destes data sources so acontece em tempo de APPLY (apos o cluster existir).
  data "aws_eks_cluster" "este" {
    name = module.eks.cluster_name
  }

  data "aws_eks_cluster_auth" "este" {
    name = module.eks.cluster_name
  }

  provider "kubernetes" {
    host                   = data.aws_eks_cluster.este.endpoint
    cluster_ca_certificate = base64decode(data.aws_eks_cluster.este.certificate_authority[0].data)
    token                  = data.aws_eks_cluster_auth.este.token
  }

  provider "helm" {
    kubernetes {
      host                   = data.aws_eks_cluster.este.endpoint
      cluster_ca_certificate = base64decode(data.aws_eks_cluster.este.certificate_authority[0].data)
      token                  = data.aws_eks_cluster_auth.este.token
    }
  }
  ```

- [ ] **Step 4: `infra/variables.tf`** — todas as entradas parametrizáveis (Aula 4/6: evitar hardcoding). `db_password` é **sensível e sem default**.

  ```hcl
  variable "region" {
    description = "Regiao AWS onde os recursos serao criados."
    type        = string
    default     = "us-east-1"
  }

  variable "project" {
    description = "Prefixo/nome do projeto usado para nomear e taguear recursos."
    type        = string
    default     = "oficina"
  }

  variable "cluster_version" {
    description = "Versao do Kubernetes no EKS."
    type        = string
    default     = "1.30"
  }

  variable "vpc_cidr" {
    description = "CIDR da VPC."
    type        = string
    default     = "10.0.0.0/16"
  }

  # --- Node group ---
  variable "node_instance_type" {
    description = "Tipo de instancia dos nos do EKS (t3.small e mais barato; t3.medium e mais folgado)."
    type        = string
    default     = "t3.medium"
  }

  variable "node_desired_size" {
    description = "Quantidade desejada de nos."
    type        = number
    default     = 2
  }

  variable "node_min_size" {
    description = "Quantidade minima de nos."
    type        = number
    default     = 1
  }

  variable "node_max_size" {
    description = "Quantidade maxima de nos."
    type        = number
    default     = 3
  }

  # --- RDS PostgreSQL ---
  variable "db_name" {
    description = "Nome do banco inicial criado no RDS."
    type        = string
    default     = "oficina"
  }

  variable "db_username" {
    description = "Usuario administrador do RDS PostgreSQL."
    type        = string
    default     = "oficina_admin"
  }

  variable "db_password" {
    description = "Senha do usuario do RDS. SENSIVEL — informe via TF_VAR_db_password ou terraform.tfvars (nao commitado)."
    type        = string
    sensitive   = true
    # SEM default de proposito: forca o operador a fornecer a senha; nunca committada.
  }

  variable "db_instance_class" {
    description = "Classe de instancia do RDS."
    type        = string
    default     = "db.t3.micro"
  }

  variable "db_allocated_storage" {
    description = "Armazenamento do RDS em GB (gp3)."
    type        = number
    default     = 20
  }

  variable "db_engine_version" {
    description = "Versao major do PostgreSQL no RDS."
    type        = string
    default     = "16"
  }

  # --- ECR / GitHub OIDC ---
  variable "ecr_repository_name" {
    description = "Nome do repositorio ECR da imagem da API."
    type        = string
    default     = "oficina-api"
  }

  variable "ecr_keep_last_images" {
    description = "Quantas imagens manter no ECR (lifecycle policy)."
    type        = number
    default     = 10
  }

  variable "github_repo" {
    description = "Repositorio GitHub (owner/nome) autorizado a assumir a role via OIDC."
    type        = string
    default     = "lcr-thiago-fernandes/fiap_15SOAT_fase1"
  }
  ```

- [ ] **Step 5: `infra/locals.tf`** — data de AZs + locals de naming/tags (Aula 5: Data/Locals).

  ```hcl
  # AZs disponiveis na regiao — usamos as 2 primeiras (custo/simplicidade).
  data "aws_availability_zones" "disponiveis" {
    state = "available"
  }

  locals {
    name         = var.project
    cluster_name = "${var.project}-eks"
    azs          = slice(data.aws_availability_zones.disponiveis.names, 0, 2)

    tags = {
      Project   = var.project
      ManagedBy = "Terraform"
      Fase      = "fase-2"
    }
  }
  ```

- [ ] **Step 6: `infra/vpc.tf`** — módulo oficial VPC (Aula 6/8), 2 AZs, subnets públicas+privadas, **single NAT**, tags exigidas pelo EKS/ELB.

  ```hcl
  # VPC via modulo oficial versionado (Aula 6 — Modulos). 2 AZs, subnets publicas + privadas.
  module "vpc" {
    source  = "terraform-aws-modules/vpc/aws"
    version = "~> 5.13"

    name = "${local.name}-vpc"
    cidr = var.vpc_cidr
    azs  = local.azs

    # /24 por AZ: privadas para nos do EKS e RDS; publicas para NAT/LoadBalancer.
    private_subnets = ["10.0.1.0/24", "10.0.2.0/24"]
    public_subnets  = ["10.0.101.0/24", "10.0.102.0/24"]

    enable_nat_gateway   = true
    single_nat_gateway   = true # CUSTO: um unico NAT Gateway para toda a VPC.
    enable_dns_hostnames = true
    enable_dns_support   = true

    # Tags exigidas pelo EKS/AWS Load Balancer Controller para descoberta de subnets.
    public_subnet_tags = {
      "kubernetes.io/role/elb" = "1"
    }
    private_subnet_tags = {
      "kubernetes.io/role/internal-elb" = "1"
    }

    tags = local.tags
  }
  ```

- [ ] **Step 7: `infra/eks.tf`** — módulo oficial EKS (Aula 6/8), 1 node group pequeno em subnets privadas, endpoint público, **access entry** admin para a role de CI.

  ```hcl
  # EKS via modulo oficial versionado (v20 — usa Access Entries nativos no lugar do aws-auth).
  module "eks" {
    source  = "terraform-aws-modules/eks/aws"
    version = "~> 20.24"

    cluster_name    = local.cluster_name
    cluster_version = var.cluster_version

    # Endpoint publico para o CI/kubectl da pessoa (demo). Nos ficam em subnets privadas.
    cluster_endpoint_public_access = true

    vpc_id     = module.vpc.vpc_id
    subnet_ids = module.vpc.private_subnets

    # Addons gerenciados (rede e DNS do cluster).
    cluster_addons = {
      coredns    = {}
      kube-proxy = {}
      vpc-cni    = {}
    }

    # Um unico managed node group pequeno (custo).
    eks_managed_node_groups = {
      default = {
        instance_types = [var.node_instance_type]
        capacity_type  = "ON_DEMAND"

        min_size     = var.node_min_size
        max_size     = var.node_max_size
        desired_size = var.node_desired_size
      }
    }

    # Controle de acesso ao cluster via Access Entries (EKS API).
    authentication_mode                      = "API_AND_CONFIG_MAP"
    enable_cluster_creator_admin_permissions = true # quem faz o apply tambem administra (kubectl local).

    access_entries = {
      # Role de CI do GitHub Actions (Plano 10): admin do cluster para poder `kubectl apply`.
      github_ci = {
        principal_arn = aws_iam_role.github_actions.arn
        type          = "STANDARD"

        policy_associations = {
          admin = {
            policy_arn = "arn:aws:iam::aws:policy/AmazonEKSClusterAdminPolicy"
            access_scope = {
              type = "cluster"
            }
          }
        }
      }
    }

    tags = local.tags
  }
  ```

  > O `access_entries` referencia `aws_iam_role.github_actions.arn` (Step 9) → o EKS depende da role. A policy de EKS da role usa `Resource = "*"` (Step 9) para **não** referenciar `module.eks` e evitar ciclo de dependência (ver Auto-revisão).

- [ ] **Step 8: `infra/rds.tf`** — PostgreSQL 16 privado, single-AZ, SG liberando 5432 **apenas** a partir do SG dos nós do EKS.

  ```hcl
  # DB subnet group nas subnets privadas (RDS nunca em subnet publica).
  resource "aws_db_subnet_group" "oficina" {
    name       = "${local.name}-db-subnet-group"
    subnet_ids = module.vpc.private_subnets
    tags       = local.tags
  }

  # Security Group do RDS: sem ingress default; libera 5432 SOMENTE do SG dos nos do EKS.
  resource "aws_security_group" "rds" {
    name        = "${local.name}-rds-sg"
    description = "Acesso ao PostgreSQL a partir dos nos do EKS"
    vpc_id      = module.vpc.vpc_id
    tags        = local.tags
  }

  resource "aws_security_group_rule" "rds_ingress_eks" {
    type                     = "ingress"
    description              = "PostgreSQL 5432 a partir dos nos do EKS"
    from_port                = 5432
    to_port                  = 5432
    protocol                 = "tcp"
    security_group_id        = aws_security_group.rds.id
    source_security_group_id = module.eks.node_security_group_id
  }

  resource "aws_security_group_rule" "rds_egress_all" {
    type              = "egress"
    description       = "Saida liberada"
    from_port         = 0
    to_port           = 0
    protocol          = "-1"
    cidr_blocks       = ["0.0.0.0/0"]
    security_group_id = aws_security_group.rds.id
  }

  resource "aws_db_instance" "oficina" {
    identifier     = "${local.name}-postgres"
    engine         = "postgres"
    engine_version = var.db_engine_version
    instance_class = var.db_instance_class

    allocated_storage = var.db_allocated_storage
    storage_type      = "gp3"
    storage_encrypted = true

    db_name  = var.db_name
    username = var.db_username
    password = var.db_password # SENSIVEL — vem de var.db_password (nunca hardcoded).

    db_subnet_group_name   = aws_db_subnet_group.oficina.name
    vpc_security_group_ids = [aws_security_group.rds.id]

    multi_az            = false # CUSTO: single-AZ.
    publicly_accessible = false # NUNCA exposto na internet.

    skip_final_snapshot = true  # demo: destroy sem snapshot final.
    deletion_protection = false # demo: permite `terraform destroy`.
    apply_immediately   = true

    tags = local.tags
  }
  ```

- [ ] **Step 9: `infra/iam-github-oidc.tf`** — OIDC provider do GitHub + role com trust **restrita ao repo** + policies mínimas (ECR push/pull, EKS describe). **Sem chaves estáticas.**

  ```hcl
  # Thumbprint do endpoint OIDC do GitHub, obtido dinamicamente (evita hardcode que expira).
  data "tls_certificate" "github" {
    url = "https://token.actions.githubusercontent.com/.well-known/openid-configuration"
  }

  # OIDC provider: permite que o GitHub Actions troque um token OIDC por credenciais AWS
  # temporarias (sem access_key/secret_key estaticos no repositorio).
  resource "aws_iam_openid_connect_provider" "github" {
    url             = "https://token.actions.githubusercontent.com"
    client_id_list  = ["sts.amazonaws.com"]
    thumbprint_list = [data.tls_certificate.github.certificates[0].sha1_fingerprint]
    tags            = local.tags
  }

  # Trust policy RESTRITA: apenas o repo configurado, com aud = sts.amazonaws.com.
  data "aws_iam_policy_document" "github_trust" {
    statement {
      effect  = "Allow"
      actions = ["sts:AssumeRoleWithWebIdentity"]

      principals {
        type        = "Federated"
        identifiers = [aws_iam_openid_connect_provider.github.arn]
      }

      condition {
        test     = "StringEquals"
        variable = "token.actions.githubusercontent.com:aud"
        values   = ["sts.amazonaws.com"]
      }

      condition {
        test     = "StringLike"
        variable = "token.actions.githubusercontent.com:sub"
        values   = ["repo:${var.github_repo}:*"]
      }
    }
  }

  resource "aws_iam_role" "github_actions" {
    name               = "${local.name}-github-actions"
    description        = "Role assumida pelo GitHub Actions via OIDC (push ECR + describe EKS)."
    assume_role_policy = data.aws_iam_policy_document.github_trust.json
    tags               = local.tags
  }

  # Policy minima 1: push/pull no ECR (o CI faz build e push da imagem).
  data "aws_iam_policy_document" "github_ecr" {
    statement {
      sid       = "EcrAuth"
      effect    = "Allow"
      actions   = ["ecr:GetAuthorizationToken"]
      resources = ["*"] # GetAuthorizationToken exige recurso "*".
    }

    statement {
      sid    = "EcrPushPull"
      effect = "Allow"
      actions = [
        "ecr:BatchCheckLayerAvailability",
        "ecr:GetDownloadUrlForLayer",
        "ecr:BatchGetImage",
        "ecr:PutImage",
        "ecr:InitiateLayerUpload",
        "ecr:UploadLayerPart",
        "ecr:CompleteLayerUpload",
      ]
      resources = [aws_ecr_repository.oficina_api.arn]
    }
  }

  # Policy minima 2: describe do EKS (o acesso ao cluster em si vem da Access Entry do EKS).
  # Resource "*" de proposito: referenciar module.eks.cluster_arn aqui criaria ciclo
  # (module.eks -> role -> module.eks), pois o EKS ja depende desta role via access_entries.
  data "aws_iam_policy_document" "github_eks" {
    statement {
      sid       = "EksDescribe"
      effect    = "Allow"
      actions   = ["eks:DescribeCluster", "eks:ListClusters"]
      resources = ["*"]
    }
  }

  resource "aws_iam_role_policy" "github_ecr" {
    name   = "${local.name}-github-ecr"
    role   = aws_iam_role.github_actions.id
    policy = data.aws_iam_policy_document.github_ecr.json
  }

  resource "aws_iam_role_policy" "github_eks" {
    name   = "${local.name}-github-eks"
    role   = aws_iam_role.github_actions.id
    policy = data.aws_iam_policy_document.github_eks.json
  }
  ```

- [ ] **Step 10: `infra/ecr.tf`** — repositório `oficina-api`, scan on push, lifecycle mantendo N imagens.

  ```hcl
  resource "aws_ecr_repository" "oficina_api" {
    name                 = var.ecr_repository_name
    image_tag_mutability = "MUTABLE"

    image_scanning_configuration {
      scan_on_push = true
    }

    tags = local.tags
  }

  # Lifecycle simples: mantem apenas as ultimas N imagens (custo/limpeza).
  resource "aws_ecr_lifecycle_policy" "oficina_api" {
    repository = aws_ecr_repository.oficina_api.name

    policy = jsonencode({
      rules = [
        {
          rulePriority = 1
          description  = "Manter apenas as ultimas ${var.ecr_keep_last_images} imagens"
          selection = {
            tagStatus   = "any"
            countType   = "imageCountMoreThan"
            countNumber = var.ecr_keep_last_images
          }
          action = {
            type = "expire"
          }
        }
      ]
    })
  }
  ```

- [ ] **Step 11: `infra/helm.tf`** — `metrics-server` no `kube-system` (necessário para o HPA do Plano 08).

  ```hcl
  # metrics-server: necessario para o HPA (autoscaling/v2) do Plano 08 funcionar.
  # Sem ele, as metricas de CPU/memoria ficam <unknown> e o HPA nao escala.
  resource "helm_release" "metrics_server" {
    name       = "metrics-server"
    namespace  = "kube-system"
    repository = "https://kubernetes-sigs.github.io/metrics-server/"
    chart      = "metrics-server"
    version    = "3.12.2"

    # No EKS o kubelet usa certificado self-signed; flag necessaria para o metrics-server coletar.
    set {
      name  = "args[0]"
      value = "--kubelet-insecure-tls"
    }

    # So instala depois do cluster + node group prontos.
    depends_on = [module.eks]
  }
  ```

- [ ] **Step 12: `infra/outputs.tf`** — saídas que alimentam o CI (Plano 10) e o Secret do K8s (Plano 08).

  ```hcl
  output "cluster_name" {
    description = "Nome do cluster EKS (usado em `aws eks update-kubeconfig`)."
    value       = module.eks.cluster_name
  }

  output "cluster_endpoint" {
    description = "Endpoint da API do cluster EKS."
    value       = module.eks.cluster_endpoint
  }

  output "ecr_repository_url" {
    description = "URL do repositorio ECR (alimenta ${var.ecr_repository_name}:TAG nos manifestos)."
    value       = aws_ecr_repository.oficina_api.repository_url
  }

  output "rds_endpoint" {
    description = "Host do RDS PostgreSQL (alimenta ConnectionStrings__Default no Secret do K8s)."
    value       = aws_db_instance.oficina.address
  }

  output "rds_port" {
    description = "Porta do RDS PostgreSQL."
    value       = aws_db_instance.oficina.port
  }

  output "github_actions_role_arn" {
    description = "ARN da role assumida pelo GitHub Actions via OIDC (secret AWS_ROLE_ARN no Plano 10)."
    value       = aws_iam_role.github_actions.arn
  }

  output "region" {
    description = "Regiao AWS dos recursos."
    value       = var.region
  }

  output "configure_kubectl" {
    description = "Comando para gerar o kubeconfig local apontando para o cluster."
    value       = "aws eks update-kubeconfig --region ${var.region} --name ${module.eks.cluster_name}"
  }
  ```

- [ ] **Step 13: `infra/terraform.tfvars.example` e `infra/.gitignore`** — exemplo com placeholders (SEM segredo) + ignore do state e do tfvars real.

  `infra/terraform.tfvars.example`:

  ```hcl
  # Copie para terraform.tfvars e ajuste. NAO commite terraform.tfvars (ver infra/.gitignore).
  # Preferivel: exportar a senha como variavel de ambiente -> export TF_VAR_db_password="..."

  region  = "us-east-1"
  project = "oficina"

  # Node group (t3.small e mais barato; t3.medium e mais folgado):
  node_instance_type = "t3.medium"
  node_desired_size  = 2
  node_min_size      = 1
  node_max_size      = 3

  # RDS:
  db_username = "oficina_admin"
  # PLACEHOLDER — NAO e uma senha real. Troque por um valor forte OU use TF_VAR_db_password.
  db_password = "TROCAR_use_uma_senha_forte_ou_TF_VAR_db_password"

  # OIDC do GitHub:
  github_repo = "lcr-thiago-fernandes/fiap_15SOAT_fase1"
  ```

  `infra/.gitignore`:

  ```gitignore
  # Terraform — estado e caches locais (NUNCA commitar).
  .terraform/
  *.tfstate
  *.tfstate.*
  crash.log
  crash.*.log

  # Arquivos de variaveis podem conter segredos (ex.: db_password) — NUNCA commitar.
  *.tfvars
  *.tfvars.json
  # ... exceto o exemplo com placeholders:
  !terraform.tfvars.example

  # Overrides locais.
  override.tf
  override.tf.json
  *_override.tf
  *_override.tf.json

  # O lock de providers (.terraform.lock.hcl) DEVE ser commitado — nao ignorar.
  ```

- [ ] **Step 14: `infra/README.md`** — recursos, bootstrap do backend, comandos, kubeconfig, custo, integração com CI/K8s.

  ````markdown
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
  ````

- [ ] **Step 15: Verificações estáticas (Git Bash, sem terraform/AWS) + gate `dotnet build`.**

  ```bash
  cd "C:/Users/thiago/source/repos/Fiap/fiap_15SOAT_fase1"

  # 0) Todos os 15 arquivos existem
  ls -1 infra/versions.tf infra/backend.tf infra/providers.tf infra/variables.tf \
        infra/locals.tf infra/vpc.tf infra/eks.tf infra/rds.tf infra/ecr.tf \
        infra/iam-github-oidc.tf infra/helm.tf infra/outputs.tf \
        infra/terraform.tfvars.example infra/.gitignore infra/README.md

  # 1) Backend S3 + lock DynamoDB presentes
  grep -n 'backend "s3"' infra/backend.tf
  grep -n 'dynamodb_table' infra/backend.tf

  # 2) Providers pinados (aws ~> 5, kubernetes, helm, tls) + terraform >= 1.5
  grep -nE 'required_version|hashicorp/aws|hashicorp/kubernetes|hashicorp/helm|hashicorp/tls' infra/versions.tf

  # 3) Providers kubernetes/helm derivam do EKS via data sources
  grep -nE 'aws_eks_cluster|aws_eks_cluster_auth' infra/providers.tf

  # 4) Modulos oficiais pinados
  grep -nE 'terraform-aws-modules/(vpc|eks)/aws' infra/vpc.tf infra/eks.tf
  grep -nE 'single_nat_gateway\s+= true' infra/vpc.tf

  # 5) EKS: endpoint publico + access entry admin p/ CI
  grep -nE 'cluster_endpoint_public_access\s+= true' infra/eks.tf
  grep -nE 'access_entries|AmazonEKSClusterAdminPolicy|github_actions' infra/eks.tf

  # 6) RDS NAO publico + SG so a partir do EKS + engine postgres 16
  grep -nE 'publicly_accessible\s+= false' infra/rds.tf
  grep -nE 'source_security_group_id\s+= module.eks.node_security_group_id' infra/rds.tf
  grep -nE 'engine\s+= "postgres"|multi_az\s+= false' infra/rds.tf

  # 7) ECR: scan on push + lifecycle
  grep -nE 'scan_on_push\s+= true' infra/ecr.tf
  grep -n 'aws_ecr_lifecycle_policy' infra/ecr.tf

  # 8) OIDC provider + role com trust restrita ao repo + condition aud
  grep -n 'aws_iam_openid_connect_provider' infra/iam-github-oidc.tf
  grep -n 'repo:${var.github_repo}:\*' infra/iam-github-oidc.tf
  grep -n 'token.actions.githubusercontent.com:aud' infra/iam-github-oidc.tf

  # 9) helm_release metrics-server no kube-system
  grep -n 'metrics-server' infra/helm.tf
  grep -nE 'namespace\s+= "kube-system"' infra/helm.tf

  # 10) outputs esperados
  grep -nE 'output "(cluster_name|cluster_endpoint|ecr_repository_url|rds_endpoint|github_actions_role_arn)"' infra/outputs.tf

  # 11) NENHUM segredo real / chave estatica commitada
  #     (esperado: SEM saida em access_key/secret_key; db_password so como VARIAVEL/placeholder)
  grep -rnE 'AKIA[0-9A-Z]{16}' infra/ && echo "FALHA: access key id encontrada" || echo "OK: sem access key id"
  grep -rnE '(access_key|secret_key)\s*=' infra/*.tf && echo "FALHA: chave estatica" || echo "OK: sem chave estatica"
  # db_password so pode aparecer como declaracao de variavel, referencia var.db_password e placeholder do example:
  grep -rn 'db_password' infra/

  # 12) .gitignore protege state e tfvars reais
  grep -nE '\*\.tfstate|\*\.tfvars|!terraform.tfvars.example' infra/.gitignore

  # 13) GATE: build C# inalterado (nada de C# muda; so arquivos novos em infra/)
  dotnet build Oficina.sln
  ```

  Checklist manual esperado:
  - 15 arquivos presentes; `backend "s3"` + `dynamodb_table`; providers pinados; kubernetes/helm via `data aws_eks_cluster*`. ✔
  - VPC/EKS por módulos versionados; `single_nat_gateway = true`; endpoint público; access entry `AmazonEKSClusterAdminPolicy` para `aws_iam_role.github_actions`. ✔
  - RDS `publicly_accessible = false`, `multi_az = false`, engine `postgres` 16, ingress 5432 **só** de `module.eks.node_security_group_id`. ✔
  - ECR `scan_on_push = true` + lifecycle; OIDC provider + role com `sub = repo:...:*` e `aud = sts.amazonaws.com`. ✔
  - `helm_release` metrics-server no `kube-system`; 5 outputs exigidos presentes. ✔
  - `grep` de `AKIA…`/`access_key`/`secret_key` **sem** matches; `db_password` só como variável sensível/placeholder. ✔
  - `dotnet build Oficina.sln` **0 erros**. ✔

- [ ] **Step 16: commit único da Task** (mensagem pt-BR).

  ```bash
  cd "C:/Users/thiago/source/repos/Fiap/fiap_15SOAT_fase1"
  git add infra/
  git commit -m "$(cat <<'EOF'
  feat(infra): IaC Terraform/AWS (EKS, RDS, ECR, OIDC, metrics-server)

  Adiciona infra/ com Terraform provisionando o alvo dos manifestos do Plano 08:
  backend remoto S3 + lock DynamoDB; VPC (2 AZs, single NAT) e EKS (node group t3.medium
  2/1/3, endpoint publico, access entry admin para a role de CI) via modulos oficiais
  versionados; RDS PostgreSQL 16 privado (db.t3.micro, single-AZ, SG 5432 so a partir do
  EKS); ECR oficina-api (scan on push + lifecycle); OIDC do GitHub Actions com trust
  restrita ao repo (sem chaves estaticas); helm_release do metrics-server para o HPA.
  Variaveis/outputs alimentam o CI (Plano 10) e o Secret do K8s (Plano 08). README documenta
  bootstrap do backend, init/plan/apply/destroy, kubeconfig e notas de custo. Nenhum segredo
  real committado (db_password sensivel; terraform.tfvars no .gitignore). Nada de C# muda.

  Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
  EOF
  )"
  ```

---

## Auto-revisão (consistência) — corrigida inline

- **Consistência de nomes/vars entre arquivos:** `var.project` → `local.name`/`local.cluster_name`, usados em VPC (`${local.name}-vpc`), EKS (`local.cluster_name`), RDS (`${local.name}-*`), IAM (`${local.name}-github-actions`) e outputs. `var.region` no provider AWS **e** no output `configure_kubectl`; o `backend.tf` repete `region = "us-east-1"` literalmente (bloco backend não aceita variável) — documentado no Step 2/README que devem bater. `var.ecr_repository_name` no recurso ECR e citado no output/README. `var.github_repo` no trust do OIDC e no `.tfvars.example`. ✔
- **Ciclo de dependência EKS ↔ role de CI (corrigido):** `module.eks.access_entries` referencia `aws_iam_role.github_actions.arn` (EKS depende da role). Se a **policy de EKS da role** referenciasse `module.eks.cluster_arn`, fecharia um ciclo. Por isso a policy `github_eks` usa `resources = ["*"]` (Step 9) — o acesso real ao cluster vem da **access entry**, não da policy IAM. Sem ciclo. ✔
- **Trust policy do OIDC restrita ao repo:** `principals` = ARN do `aws_iam_openid_connect_provider.github`; `condition StringEquals aud = sts.amazonaws.com`; `condition StringLike sub = repo:${var.github_repo}:*` (default `lcr-thiago-fernandes/fiap_15SOAT_fase1`). Não é `*` aberto. ✔
- **SG do RDS só a partir do EKS:** o único ingress (`aws_security_group_rule.rds_ingress_eks`) usa `source_security_group_id = module.eks.node_security_group_id` na porta 5432 — sem `cidr_blocks` abertos no ingress. `publicly_accessible = false` e subnets privadas reforçam. ✔
- **Nenhum `access_key`/senha commitada:** provider AWS sem credenciais (vem do ambiente/OIDC); `db_password` é `variable` **sensível sem default**; `.tfvars.example` só tem placeholder `TROCAR_...`; `infra/.gitignore` ignora `*.tfvars` (exceto `*.example`) e `*.tfstate`. O grep do Step 15.11 falha o gate se achar `AKIA…`/`access_key`/`secret_key`. ✔
- **Providers kubernetes/helm e chicken-and-egg do cluster:** `data.aws_eks_cluster*` usam `name = module.eks.cluster_name` (valor computado) → leitura adiada para o **apply**, após o cluster existir; não quebra o `plan` inicial. `helm_release` tem `depends_on = [module.eks]`. ✔
- **metrics-server ↔ HPA do Plano 08:** o HPA (`autoscaling/v2`, CPU 60% / mem 70%) depende de métricas; o `helm_release` provê o metrics-server no `kube-system`. Fecha a dependência declarada no `k8s/README.md`. ✔
- **RDS ↔ Secret do K8s:** output `rds_endpoint` (host) + `rds_port` + `db_name`/`db_username`/`db_password` compõem a `ConnectionStrings__Default` esperada pelo `k8s/secret.yaml` (`Host=...;Port=5432;Database=oficina;Username=...;Password=...`). `db_name` default `oficina` bate com o `Database=oficina` do template do Plano 08. ✔
- **ECR ↔ manifestos:** `ecr_repository_name` default `oficina-api` bate com o `oficina-api:ci` do CI atual e com `${ECR_REPOSITORY}` do Plano 08; `ecr_repository_url` (output) alimenta a substituição do CI. ✔
- **`nada de C# muda`:** só arquivos novos em `infra/`; `dotnet build Oficina.sln` no Step 15.13 deve seguir 0 erros. ✔

## Riscos / ambiguidades a revisar

1. **Quotas/limites AWS:** contas novas podem ter limite baixo de vCPU on-demand, Elastic IPs (NAT usa 1) ou VPCs por região. Se o `apply` falhar por quota, solicitar aumento ou reduzir `node_desired_size`/trocar para `t3.small`.
2. **Custo:** ~US$180/mês se ficar ligado (EKS + 2 nós + NAT + RDS + ELB). Mitigado por single NAT, single-AZ e node group pequeno — **mas a mitigação real é `terraform destroy` após a demo** (README).
3. **Token do `aws_eks_cluster_auth` expira (~15 min):** para `apply`/`destroy` longos o token pode expirar entre a criação do cluster e o `helm_release`. Alternativa mais robusta (se der problema): trocar os providers `kubernetes`/`helm` para o bloco `exec` (`aws eks get-token`) em vez do `token` do data source. Mantido o `data` por ser o que o curso (Aula 2) e a spec pedem.
4. **Access entry (EKS v20) vs `aws-auth` (legado):** o plano usa `authentication_mode = "API_AND_CONFIG_MAP"` + `access_entries` (v20). Se a organização exigir o ConfigMap `aws-auth` clássico, seria preciso o submódulo `aws-auth` — não é o caso aqui (v20 nativo cobre o CI). Confirmar que a conta suporta EKS 1.30 + access entries (padrão desde 2024).
5. **Destroy travado por ELB órfão:** o `Service type: LoadBalancer` do Plano 08 cria um ELB fora do Terraform; se não for removido antes, o `destroy` da VPC/subnets falha. README instrui `kubectl delete -f ../k8s/service.yaml` antes do `terraform destroy`.
6. **Nome do bucket S3 global:** `oficina-tfstate-fiap-15soat` pode já existir (namespace S3 é global). Se `create-bucket` falhar por conflito, ajustar o sufixo **no bootstrap e no `backend.tf`** simultaneamente.
7. **`t3.small` × densidade de pods:** default é `t3.medium` (alinhado à Aula 8, evita falha de scheduling com coredns+metrics-server+2 réplicas+HPA). `t3.small` reduz custo mas limita pods por nó (limite de ENI/IP do vpc-cni) — usar só se o orçamento apertar e monitorar `Pending` pods.
8. **Versão dos módulos/chart:** pins `vpc ~> 5.13`, `eks ~> 20.24`, chart `metrics-server 3.12.2` refletem linhas estáveis atuais; se o Registry tiver avançado muito, o operador pode precisar ajustar argumentos (ex.: renome de outputs). `terraform init` baixa a versão compatível dentro do range.
