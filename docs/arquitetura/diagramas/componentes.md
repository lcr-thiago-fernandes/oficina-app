# Diagrama de componentes — Fase 3

Visão de nuvem dos quatro repositórios em execução. A cor não tem significado; as caixas
`subgraph` são limites de rede. Quem cria cada recurso está em
[RFC-004](../../rfc/RFC-004-topologia-quatro-repositorios.md).

```mermaid
flowchart TB
    CLI["Cliente / Atendente<br/>HTTPS"]
    NR["New Relic<br/>APM · nri-bundle · layer da Lambda"]

    subgraph AWS["AWS — us-east-1"]
        APIGW["API Gateway HTTP API<br/>stage $default<br/>throttling 10 rps / burst 20 em POST /auth/*"]
        AUTHZ["Lambda oficina-auth-authorizer<br/>.NET 8 · fora da VPC · cache 300 s"]
        SM["Secrets Manager<br/>oficina/jwt_secret<br/>oficina/db_password<br/>oficina/newrelic_license_key"]
        SSM["SSM Parameter Store<br/>/oficina/network/* · /oficina/apigw/*<br/>/oficina/db/* · /oficina/eks/* · /oficina/ecr/*"]
        DDB[("DynamoDB<br/>oficina-auth-tentativas")]
        ECR["ECR<br/>oficina-api"]

        subgraph VPC["VPC — 2 AZs"]
            NAT["NAT Gateway<br/>subnet pública"]
            VPCL["VPC Link"]
            NLB["NLB interno<br/>:80 → 30080 (prd)<br/>:81 → 30081 (hml, só na VPC)"]
            AUTHAPI["Lambda oficina-auth-api<br/>.NET 8 · subnets privadas · oficina-lambda-auth-sg"]
            PRD["ns oficina-prd<br/>Deployment · Service NodePort 30080<br/>HPA 2–10 · CPU 60%"]
            HML["ns oficina-hml<br/>Deployment · Service NodePort 30081<br/>HPA 2–10 · CPU 60%"]
            RDS[("RDS PostgreSQL 16<br/>oficina-postgres · db.t3.micro<br/>subnet privada")]
        end
    end

    CLI --> APIGW
    APIGW -->|"ANY /api/v1/{proxy+}"| AUTHZ
    APIGW -->|"POST /auth/cliente · POST /auth/admin"| AUTHAPI
    APIGW -->|"integração HTTP_PROXY"| VPCL
    VPCL --> NLB
    NLB --> PRD
    NLB --> HML
    AUTHAPI --> RDS
    AUTHAPI --> DDB
    AUTHAPI --> NAT
    AUTHZ --> SM
    NAT --> SM
    PRD --> RDS
    HML --> RDS
    ECR -->|"imagem"| PRD
    ECR -->|"imagem"| HML
    SSM -.->|"contrato entre repositórios"| AUTHAPI
    SSM -.->|"contrato entre repositórios"| RDS
    PRD -.-> NR
    AUTHAPI -.-> NR
    AUTHZ -.-> NR
```

## Quem cria o quê

| Componente | Repositório |
|---|---|
| VPC, NAT, EKS, node group, ECR, NLB, VPC Link, API Gateway, roles OIDC, New Relic (Helm, dashboards, alertas) | `oficina-infra-k8s` |
| RDS, DB subnet group, security group do RDS, parameter group, `oficina/db_password` | `oficina-infra-db` |
| `oficina-auth-api`, `oficina-auth-authorizer`, rotas `/auth/*` e `ANY /api/v1/{proxy+}`, DynamoDB de tentativas | `oficina-lambda-auth` |
| Imagem da API, manifests dos dois namespaces, migrations | `oficina-app` |

O `NLB` desenha as duas setas para `PRD` e `HML`, mas só `:80` é alcançável pelo API Gateway;
homologação exige estar dentro da VPC ([ADR-018](../ADR-018-ambientes-por-namespace.md)).
