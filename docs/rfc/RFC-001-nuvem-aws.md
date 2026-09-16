# RFC-001 — Escolha da nuvem: AWS

**Status:** Aceita
**Data:** 2026-09-15
**Autor:** Thiago Fernandes
**ADRs relacionados:** ADR-010, ADR-014, ADR-016, ADR-018

## 1. Contexto e problema

A Fase 2 já colocou a `oficina-app` inteira em produção na AWS: EKS, RDS PostgreSQL,
ECR e OIDC do GitHub Actions, tudo em Terraform (ver
[ADR-010](../arquitetura/ADR-010-aws-eks-rds-terraform.md)). A Fase 3 acrescenta três
peças novas — um API Gateway na frente da aplicação, autenticação serverless (Lambda)
e uma plataforma de observabilidade — e cada uma delas existe, com nomes e preços
diferentes, nos três grandes provedores. A pergunta deste RFC não é "qual nuvem é
melhor em abstrato", mas se vale a pena migrar uma base já funcional para ganhar
alguma dessas peças, ou se a Fase 3 deve simplesmente estender o que já está no ar.

## 2. Alternativas consideradas

| Critério | AWS | Azure | GCP |
|---|---|---|---|
| Kubernetes gerenciado | EKS (já em produção desde a Fase 2) | AKS | GKE |
| Gateway de API | API Gateway HTTP API | API Management | API Gateway |
| Serverless para .NET 8 | Lambda (suporte de primeira classe ao runtime `dotnet8`) | Functions | Cloud Functions/Run |
| Custo do control plane gerenciado | US$ 73,00/mês (EKS, fixo) | cobrança por AKS varia por SKU do plano | GKE Autopilot/Standard, cobrança por cluster ou por pod |
| Custo de migração do que já existe | **zero** — Terraform, imagem Docker, pipelines e roles OIDC da Fase 2 continuam válidos | reescrever todo o Terraform de rede/cluster/banco, recriar pipelines e a federação OIDC | idem Azure |

O critério que decide não é nenhuma linha isolada da tabela acima — é a última: o
custo de migrar Terraform, imagem, pipelines e roles OIDC já escritos e validados na
Fase 2. Esse custo é pago tanto em Azure quanto em GCP e é zero em AWS.

## 3. Decisão

Permanecer na **AWS**. Motivo principal: a Fase 2 inteira já está em Terraform da AWS
(EKS, RDS, ECR, OIDC — [ADR-010](../arquitetura/ADR-010-aws-eks-rds-terraform.md)) e
migrar para outro provedor consumiria o orçamento de tempo da Fase 3 reescrevendo
infraestrutura que já funciona, sem entregar nenhum item avaliado na disciplina.
Motivo secundário: dentro da própria AWS, o HTTP API do API Gateway v2 é o gateway
mais barato dos três serviços comparados para o volume desta oficina — sem WAF nativo,
sem chaves de API por cliente, sem modelos de request/response, nenhum dos quais este
escopo usa ([ADR-014](../arquitetura/ADR-014-api-gateway-http-api.md)) — e a Lambda
.NET 8 tem suporte de primeira classe (runtime gerenciado, sem camada de compatibilidade).

## 4. Consequências

- ✅ Zero retrabalho de infraestrutura como código: o Terraform, a imagem Docker e os
  pipelines de CI/CD da Fase 2 continuam válidos e são só estendidos, não reescritos.
- ✅ Um único provedor de identidade para o CI/CD dos quatro repositórios: OIDC do
  GitHub Actions → IAM da AWS, sem um segundo conjunto de credenciais de nuvem para
  gerenciar.
- ⚠️ Lock-in real: VPC Link, Lambda Authorizer com resposta simples e SSM Parameter
  Store como contrato entre repositórios (ver
  [RFC-004](RFC-004-topologia-quatro-repositorios.md)) não têm equivalente direto em
  Azure ou GCP — uma eventual migração de provedor não seria uma troca de nomes, seria
  um redesenho.
- ⚠️ Custo estimado de **~US$ 196,00/mês** com todo o ambiente da Fase 3 no ar, pela
  tabela de custo da seção 4 ("Custo estimado") do
  [`fase3-design-arquitetural.md`](../arquitetura/fase3-design-arquitetural.md):

  | Item | US$/mês |
  |---|---|
  | EKS control plane | 73,00 |
  | 2× t3.medium (nós) | ~60,00 |
  | NAT Gateway (1) | ~32,00 |
  | RDS db.t3.micro + 20GB | ~15,00 |
  | NLB interno | ~16,00 |
  | **Total** | **~196,00** |

  Este valor é uma **estimativa** derivada da AWS Pricing Calculator anexada a este
  RFC, não uma fatura real: o ambiente descrito nesta documentação **não foi aplicado
  na AWS** até a data acima. `terraform destroy` fora das janelas de demonstração
  reduz o custo a praticamente zero, porque nenhum dos itens da tabela é cobrado por
  reserva — todos são recursos sob demanda que somem com o `destroy`.

## 5. Como isto está implementado

| O quê | Onde |
|---|---|
| Root Terraform de VPC, EKS, ECR, NLB, API Gateway, VPC Link, New Relic | `oficina-infra-k8s/terraform/` (`eks.tf`, `apigw.tf`, `nlb.tf`, `vpc.tf`, `ecr.tf`) |
| Root Terraform do RDS PostgreSQL | `oficina-infra-db/terraform/` (`rds.tf`) |
| Root Terraform da autenticação serverless (Lambda + Authorizer) | `oficina-lambda-auth/terraform/` (`lambda-auth-api.tf`, `lambda-authorizer.tf`, `apigw.tf`) |
| CD lê identificadores de infraestrutura do SSM e credenciais via OIDC para AWS | `oficina-app/.github/workflows/cd.yml` (passo "Configurar credenciais AWS (OIDC)", linha 75; passo "Ler parametros do SSM", linhas 85-101) |
| Decisão original de provisionar em AWS (Fase 2) | `oficina-app/docs/arquitetura/ADR-010-aws-eks-rds-terraform.md` |
| Tabela de custo estimado (origem dos valores desta seção 4) | `oficina-app/docs/arquitetura/fase3-design-arquitetural.md`, seção "Custo estimado" |
