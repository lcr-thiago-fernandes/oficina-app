# Documentação arquitetural

O design completo da Fase 3 está em
[fase3-design-arquitetural.md](fase3-design-arquitetural.md); os RFCs, em [../rfc](../rfc/README.md).
Onde os dois divergem, valem os ADRs e RFCs — eles descrevem o que foi implementado.

## Diagramas

| Diagrama | Arquivo |
|---|---|
| Componentes (visão de nuvem) | [diagramas/componentes.md](diagramas/componentes.md) |
| Sequência — autenticação por CPF (200/400/403/404/429) | [diagramas/sequencia-autenticacao-cpf.md](diagramas/sequencia-autenticacao-cpf.md) |
| Sequência — abertura de ordem de serviço | [diagramas/sequencia-abertura-os.md](diagramas/sequencia-abertura-os.md) |
| Entidade-relacionamento | [diagramas/entidade-relacionamento.md](diagramas/entidade-relacionamento.md) |

## ADRs

| # | Assunto | Status |
|---|---|---|
| [ADR-001](ADR-001-clean-architecture.md) | Clean Architecture | Aceita |
| [ADR-002](ADR-002-postgresql.md) | PostgreSQL | Aceita |
| [ADR-003](ADR-003-jwt-bootstrap-admin.md) | Autenticação JWT e bootstrap do admin | Superseded por ADR-015 |
| [ADR-004](ADR-004-multiplos-bounded-contexts.md) | Múltiplos bounded contexts | Aceita |
| [ADR-005](ADR-005-snapshot-precos-os.md) | Snapshot de preços na OS | Aceita |
| [ADR-006](ADR-006-saldo-denormalizado-estoque.md) | Saldo desnormalizado de estoque | Aceita |
| [ADR-007](ADR-007-linguagem-portugues.md) | Linguagem do código em português | Aceita |
| [ADR-008](ADR-008-soft-delete.md) | Soft delete | Aceita |
| [ADR-009](ADR-009-refatoracao-clean-architecture.md) | Refatoração Clean Architecture | Aceita |
| [ADR-010](ADR-010-aws-eks-rds-terraform.md) | AWS EKS + RDS via Terraform | Aceita |
| [ADR-011](ADR-011-webhook-aprovacao-token.md) | Webhook de aprovação por token | Aceita |
| [ADR-012](ADR-012-migracao-via-job-kubernetes.md) | Migração via Job do Kubernetes | Aceita |
| [ADR-013](ADR-013-observabilidade-minima.md) | Observabilidade mínima (OpenTelemetry) | Aceita |
| [ADR-014](ADR-014-api-gateway-http-api.md) | API Gateway HTTP API + VPC Link | Aceita |
| [ADR-015](ADR-015-lambda-authorizer-hs256.md) | Lambda Authorizer HS256 | Aceita |
| [ADR-016](ADR-016-nlb-terraform-nodeport.md) | NLB pelo Terraform + Service NodePort | Aceita |
| [ADR-017](ADR-017-escalabilidade-hpa.md) | Escalabilidade horizontal via HPA | Aceita |
| [ADR-018](ADR-018-ambientes-por-namespace.md) | Ambientes por namespace | Aceita |
| [ADR-019](ADR-019-duplicacao-documento.md) | Duplicação consciente do `Documento.cs` | Aceita |
| [ADR-020](ADR-020-historico-status.md) | Histórico de status como tabela dedicada | Aceita |
