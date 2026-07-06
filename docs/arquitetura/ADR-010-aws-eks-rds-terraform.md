# ADR-010 — Orquestração em AWS EKS + RDS via Terraform

**Status:** Aceita
**Data:** 2026-07-06

## Contexto

A Fase 2 exige empacotar a aplicação em contêiner, orquestrar em Kubernetes e provisionar
a infraestrutura como código, demonstrando escalabilidade. Era preciso escolher provedor
de nuvem, serviço de Kubernetes gerenciado, banco gerenciado e ferramenta de IaC.

## Decisão

Provisionar em **AWS** via **Terraform** (`infra/`):

- **Amazon EKS** — Kubernetes gerenciado (control plane + 1 node group `t3.medium`, 2–3 nós),
  addons e uma *access entry* concedendo admin do cluster ao role de CI.
- **Amazon RDS PostgreSQL 16** — `db.t3.micro`, single-AZ, privado (5432 só do SG dos nós).
- **Amazon ECR** — repositório `oficina-api` (scan on push, retém as 10 últimas imagens).
- **VPC** (módulo oficial) — 2 AZs, subnets públicas/privadas, **NAT único** (economia).
- **OIDC do GitHub Actions** — roles assumidos pelo CI/CD sem chave estática.
- **metrics-server** (Helm) — necessário para o HPA.

Estado remoto em **S3 + DynamoDB** (lock). O provisionamento inicial é **local**
(`cd infra && terraform init/apply`) por causa do *chicken-and-egg* do role admin.

## Consequências

- ✅ Kubernetes gerenciado, sem operar control plane
- ✅ Banco gerenciado (backups, patching) e isolado em subnet privada
- ✅ IaC versionada e reproduzível; custo previsível e destruível pós-demo
- ✅ Autenticação do CI/CD por OIDC (sem segredo de longa duração)
- ⚠️ Recursos pagos (EKS/NAT/RDS/ELB ~US$180/mês se ficarem ligados) — **destruir após a demo**
- ⚠️ Single-AZ/NAT único reduzem custo mas também a resiliência; aceitável para MVP acadêmico
