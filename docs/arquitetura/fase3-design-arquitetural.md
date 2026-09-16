# Fase 3 — Design Arquitetural

**Data:** 2026-09-14
**Status:** Aprovado
**Projeto:** Tech Challenge Fase 3 — Pós-Tech FIAP (15SOAT) — Oficina Mecânica

**Nota de versionamento:** este é o design **aprovado antes da execução**, preservado como registro
histórico. Onde a implementação divergiu, valem os ADRs e RFCs — as divergências estão registradas em
[ADR-014](ADR-014-api-gateway-http-api.md), [ADR-015](ADR-015-lambda-authorizer-hs256.md),
[ADR-017](ADR-017-escalabilidade-hpa.md), [ADR-018](ADR-018-ambientes-por-namespace.md),
[ADR-020](ADR-020-historico-status.md), [RFC-002](../rfc/RFC-002-banco-rds-postgresql.md),
[RFC-003](../rfc/RFC-003-autenticacao-cpf-jwt.md),
[RFC-004](../rfc/RFC-004-topologia-quatro-repositorios.md) e
[RFC-005](../rfc/RFC-005-observabilidade-new-relic.md).

---

## 1. Contexto e objetivo

A oficina expandiu para múltiplas unidades. A Fase 3 eleva o sistema a operação
corporativa: controle de acesso por CPF, API Gateway, arquitetura serverless para
autenticação, segregação em quatro repositórios com CI/CD independente,
observabilidade completa e documentação arquitetural formal.

A base é o entregável da Fase 2: API .NET 8 em Clean Architecture, EKS, RDS
PostgreSQL 16, Terraform e GitHub Actions, hoje num único repositório.

### Decisões fundadoras (travadas antes do design)

| Decisão | Escolha |
|---|---|
| Nuvem | AWS |
| API Gateway | AWS API Gateway (HTTP API) |
| Autenticação | Lambda própria emitindo JWT + Lambda Authorizer |
| Runtime da Lambda | .NET 8 |
| Observabilidade | New Relic |
| Acesso da Lambda ao banco | Dentro da VPC, subnet privada |
| Split de repositórios | 4 repositórios novos |
| Ambientes | 1 cluster EKS, 2 namespaces (hml/prd) |
| Modelo de auth | Lambda como emissor único (perfis Cliente e Admin/Atendente) |
| Notificações serverless | Fora de escopo |
| Modelagem do banco | Ajustes reais + ER + RFC |

### Premissas

Nada existe previamente: conta New Relic, infraestrutura AWS, backend do Terraform
e os quatro repositórios são criados do zero. O repositório atual
(`fiap_15SOAT_fase3`) é congelado como referência histórica.

---

## 2. Topologia dos repositórios

| Repo | Conteúdo | Deploy |
|---|---|---|
| `oficina-infra-k8s` | VPC, subnets, NAT, EKS + node group, ECR, roles OIDC, API Gateway (HTTP API), NLB interno, VPC Link, metrics-server, New Relic (Helm + dashboards + alertas) | `terraform apply` |
| `oficina-infra-db` | RDS PostgreSQL 16, DB subnet group, security group, parameter group | `terraform apply` |
| `oficina-lambda-auth` | Lambda .NET 8 de autenticação por CPF, Lambda Authorizer, rotas `/auth/*` | `terraform apply` |
| `oficina-app` | API .NET 8 (Clean Architecture), manifests K8s, migrations, documentação arquitetural | build → ECR → `kubectl apply` |

### Contrato entre repositórios

Nenhum repositório lê o state do outro. O contrato é **SSM Parameter Store** para
identificadores e **AWS Secrets Manager** para valores sensíveis.

```
/oficina/network/vpc_id                  ← infra-k8s
/oficina/network/private_subnet_ids      ← infra-k8s
/oficina/network/eks_node_sg_id          ← infra-k8s
/oficina/apigw/api_id                    ← infra-k8s
/oficina/apigw/vpc_link_id               ← infra-k8s
/oficina/eks/cluster_name                ← infra-k8s
/oficina/ecr/repository_url              ← infra-k8s
/oficina/db/endpoint                     ← infra-db
/oficina/db/security_group_id            ← infra-db
/oficina/auth/lambda_authorizer_id       ← lambda-auth
```

Secrets Manager: `oficina/db_password`, `oficina/jwt_secret`,
`oficina/newrelic_license_key`.

Justificativa da escolha (SSM em vez de `terraform_remote_state`) em RFC-004:
evita conceder acesso cruzado a buckets de state, mantém os repositórios
independentes e torna a ordem de apply explícita em vez de implícita.

### Ordem de apply

```
1. scripts/bootstrap.sh   (S3 + DynamoDB + OIDC provider + role de infra)
2. oficina-infra-k8s
3. oficina-infra-db
4. oficina-lambda-auth
5. oficina-app
```

`terraform destroy` roda na ordem inversa. Documentado no README de cada repo.

### Dependência circular resolvida

Na Fase 2, `rds.tf` referencia `module.eks.node_security_group_id` diretamente.
Separado em dois repositórios isso se torna impossível na direção original.
Solução: `infra-db` descobre o security group dos nós por tag
(`aws:eks:cluster-name = oficina-eks`) via data source e cria ele mesmo a regra de
ingress na porta 5432. Funciona porque `infra-db` sempre roda depois de `infra-k8s`.

---

## 3. Autenticação

### Componentes

| Função | Runtime | VPC | Responsabilidade |
|---|---|---|---|
| `oficina-auth-api` | .NET 8, Minimal API sobre Lambda | sim (subnet privada) | `POST /auth/cliente`, `POST /auth/admin` — valida, consulta o RDS, emite JWT |
| `oficina-auth-authorizer` | .NET 8, handler puro | não | Valida assinatura/expiração do JWT, devolve policy ao API Gateway |

O authorizer fica **fora da VPC** deliberadamente: ele apenas verifica a assinatura
HS256 e não acessa o banco. Lambda em VPC paga criação de ENI e cold start maior, e
o authorizer é invocado em toda requisição protegida. Cache habilitado
(`authorizer_result_ttl_in_seconds = 300`, chave = header `Authorization`).

### Tabela de rotas do API Gateway

| Rota | Integração | Authorizer |
|---|---|---|
| `POST /auth/cliente` | Lambda `auth-api` | — |
| `POST /auth/admin` | Lambda `auth-api` | — |
| `GET /health` | VPC Link → NLB → EKS | — |
| `GET /swagger/{proxy+}` | VPC Link → NLB → EKS | — |
| `POST /api/v1/ordens-servico/{id}/orcamento/aprovacao` | VPC Link → NLB → EKS | — (rota mais específica; segue protegida por `X-Webhook-Token`) |
| `ANY /api/v1/{proxy+}` | VPC Link → NLB → EKS | Lambda Authorizer |

### Contrato do JWT

```json
{
  "iss": "oficina-auth",
  "aud": "oficina-api",
  "sub": "<guid do cliente ou do usuário>",
  "perfil": "Cliente | Atendente | Admin",
  "documento": "12345678901",
  "nome": "Fulano de Tal",
  "jti": "<guid>",
  "exp": "<now + 60min>"
}
```

`documento` presente apenas no perfil `Cliente`. Algoritmo HS256, secret no Secrets
Manager, expiração de 60 minutos.

O authorizer injeta `perfil`, `sub` e `documento` no `requestContext.authorizer`; o
API Gateway repassa como headers `X-Perfil`, `X-Sub`, `X-Documento`. **A API continua
validando o JWT por conta própria** (defesa em profundidade: alcançar o NLB por
dentro da VPC não basta para acessar rotas protegidas).

### Fluxo — `POST /auth/cliente`

```
cliente → API Gateway → Lambda auth-api
    1. Documento.Criar(cpf)                   → 400 se dígito verificador inválido
    2. SELECT cliente WHERE documento = @cpf  → 404 se não existe
                                              → 403 se Ativo = false
    3. lê jwt_secret do Secrets Manager (cache estático em memória)
    4. assina HS256                           → 200 { access_token, expires_in, perfil }
```

Os três caminhos de erro correspondem exatamente aos três verbos do enunciado:
validar o CPF, consultar a existência, consultar o status.

### Mudanças na aplicação

1. `AuthController` (`api/v1/auth/login`) é **removido** — a Lambda é o emissor único.
2. `ConfiguracaoJwt` passa a validar `iss=oficina-auth`, `aud=oficina-api`.
3. Nova policy `RequerCliente` e endpoints de autoatendimento:
   - `GET /api/v1/me/ordens-servico` — OS do cliente identificado no token
   - `GET /api/v1/me/veiculos` — veículos do cliente identificado no token
4. `GET /api/v1/consulta/{numeroOs}` deixa de ser `[AllowAnonymous]` e passa a exigir
   perfil `Cliente` — materializa "proteger rotas sensíveis com autenticação via CPF".
   A autorização é por **propriedade**: além do perfil, a OS consultada precisa
   pertencer ao cliente identificado na claim `sub`; caso contrário retorna 404 (não
   403, para não revelar a existência da OS).
5. Rotas de gestão (`clientes`, `pecas`, `servicos`, `ordens-servico`) seguem exigindo
   `Admin`/`Atendente`.
6. O HPA da Fase 2 (2–10 réplicas, gatilho de CPU em 70%) é mantido sem alteração e
   passa a existir nos dois namespaces. Decisão formalizada em ADR-017.

### Reuso de código

`Documento.cs` (~90 linhas, sem dependências externas) é **copiado** para o repositório
da Lambda, com teste de paridade garantindo que ambas as cópias aceitam e rejeitam o
mesmo conjunto de CPFs. Duplicação consciente, registrada em ADR-019: o custo do
acoplamento (pacote NuGet privado, versionamento cruzado, CI encadeado) supera o
custo da cópia.

---

## 4. Rede, exposição do cluster e acesso ao banco

### O problema do VPC Link

VPC Link de HTTP API exige o ARN do listener de um ALB/NLB no momento do
`terraform apply`. Se o Kubernetes cria o load balancer (`Service type: LoadBalancer`),
esse ARN só existe após o deploy da aplicação — dependência circular.

### Solução: Terraform é dono do NLB

```
oficina-infra-k8s cria:
  - NLB interno nas subnets privadas
  - Target Group (type: instance, porta 30080, health check HTTP em /health)
  - Listener :80 → Target Group
  - aws_autoscaling_attachment → ASG do node group do EKS
  - VPC Link + Integration (HTTP_PROXY, connection_type = VPC_LINK)

oficina-app deploya:
  - Service type: NodePort com nodePort: 30080 fixo
```

O ARN do listener é conhecido no `plan`, a ordem de apply é linear e o Service do K8s
se reduz a poucas linhas.

Alternativa descartada: AWS Load Balancer Controller + `TargetGroupBinding` com
targets `ip` (um hop a menos, health check direto no pod). Exige IRSA, Helm chart e
IAM policy adicionais — complexidade desproporcional ao contexto. Registrado em
ADR-016 com o caminho de evolução.

### Security groups

| SG | Ingress | Egress |
|---|---|---|
| `sg-vpclink` | — | TCP 30080 → `sg-nodes` |
| `sg-nodes` (EKS) | TCP 30080 ← `sg-vpclink` | all |
| `sg-lambda-auth` | — | TCP 443 (Secrets Manager via NAT), TCP 5432 → `sg-rds` |
| `sg-rds` | TCP 5432 ← `sg-nodes`, TCP 5432 ← `sg-lambda-auth` | all |

RDS permanece `publicly_accessible = false` em subnet privada.

### Lambda na VPC

`oficina-auth-api` roda nas duas subnets privadas com `sg-lambda-auth`, saindo para o
Secrets Manager pelo NAT Gateway existente. Sem VPC Endpoint de interface (custo
adicional sem benefício, o NAT já está provisionado).

Mitigações de cold start:

- `PublishReadyToRun=true` no publish — elimina JIT no startup
- `MaxPoolSize=2` no Npgsql — Lambda atende um request por container
- secret cacheado em campo `static` fora do handler
- memória em 512MB (CPU é proporcional à memória na Lambda)

### RDS — mudanças em relação à Fase 2

Mantém `db.t3.micro`, 20GB gp3, single-AZ, criptografado. Acrescenta:

- parameter group customizado com `log_min_duration_statement = 500`
- Performance Insights habilitado (retenção de 7 dias, gratuita)
- `backup_retention_period = 1`
- `deletion_protection = false` e `skip_final_snapshot = true` mantidos (ambiente
  precisa ser destruível)

### Custo estimado

| Item | US$/mês |
|---|---|
| EKS control plane | 73,00 |
| 2× t3.medium | ~60,00 |
| NAT Gateway (1) | ~32,00 |
| RDS db.t3.micro + 20GB | ~15,00 |
| NLB interno | ~16,00 |
| API Gateway + Lambda | ~0 (free tier) |
| New Relic | 0 (free tier 100GB) |
| **Total** | **~196,00** |

`terraform destroy` fora das janelas de demonstração reduz o custo a praticamente
zero. Estimativa detalhada na AWS Pricing Calculator anexada à RFC-001.

---

## 5. Observabilidade (New Relic)

### Instrumentação

| Alvo | Mecanismo | Repositório |
|---|---|---|
| API .NET | Agente APM .NET no Dockerfile (`CORECLR_ENABLE_PROFILING=1` + profiler) | `oficina-app` |
| Cluster K8s | Helm chart `nri-bundle` | `oficina-infra-k8s` |
| Lambdas | Camada `NewRelicLambdaExtension` + `NEW_RELIC_LAMBDA_HANDLER` | `oficina-lambda-auth` |

`NEW_RELIC_LICENSE_KEY` no Secrets Manager, injetada como Secret do K8s pelo CD e
como variável de ambiente da Lambda pelo Terraform.

### Logs e correlação

1. **`trace.id` / `span.id`** — injetados automaticamente pelo agente APM em cada
   linha de log (*logs in context*).
2. **`correlationId`** — middleware novo na API: lê `X-Correlation-Id` (ou o
   `requestId` do API Gateway), gera um se ausente, empurra no `LogContext` do Serilog
   e devolve no header da resposta. A Lambda de auth propaga o mesmo header.

Encaminhamento pelo agente **e** Fluent Bit simultaneamente causa log duplicado e
cobrança dobrada. Configuração adotada: o agente encaminha os logs da aplicação; o
Fluent Bit do `nri-bundle` roda com `exclude` nos namespaces `oficina-*` e cuida
apenas dos logs de sistema do cluster.

### Métricas de negócio

Custom event emitido a cada transição da máquina de estados da OS, na mesma
transação da escrita em `os.historico_status`:

```
OrdemServicoEvento {
  numeroOs, statusAnterior, statusNovo,
  duracaoNoStatusSegundos, resultado: "Sucesso" | "Falha", unidade
}
```

Queries dos painéis:

```sql
-- Volume diário de OS
SELECT count(*) FROM OrdemServicoEvento
WHERE statusNovo = 'Recebida' TIMESERIES 1 day SINCE 30 days ago

-- Tempo médio de execução por status
SELECT average(duracaoNoStatusSegundos)/60 AS 'minutos'
FROM OrdemServicoEvento FACET statusAnterior

-- Erros e falhas nas integrações
SELECT count(*) FROM TransactionError
WHERE appName = 'oficina-api' FACET error.class, transactionName
```

### Cobertura dos requisitos

| Requisito | Mecanismo |
|---|---|
| Latência das APIs | APM — `Transaction` p50/p95/p99 por endpoint |
| CPU/memória do K8s | `nri-bundle` — dashboard nativo de Kubernetes |
| Healthchecks e uptime | `K8sPodSample` (pods prontos, `restartCount`) + alert de *loss of signal* |
| Alertas p/ falhas no processamento de OS | NRQL sobre `OrdemServicoEvento WHERE resultado='Falha'` e `TransactionError` nas rotas de OS |
| Logs JSON com correlação | Serilog + agente (`trace.id`) + middleware (`correlationId`) |

### Alertas e dashboard como código

Provider `newrelic` no `oficina-infra-k8s`, pasta `newrelic/`.

| Alerta | Condição |
|---|---|
| `Oficina-API-Prod-Latencia-Critical` | p95 > 500ms por 5 min |
| `Oficina-API-Prod-TaxaErro-Critical` | erro > 5% por 5 min |
| `Oficina-API-Prod-Uptime-Critical` | loss of signal > 5 min |
| `Oficina-OS-Prod-FalhaProcessamento-Critical` | `OrdemServicoEvento WHERE resultado='Falha'` > 0 |
| `Oficina-EKS-Prod-CPU-Warning` | CPU do node > 80% por 10 min |

Um dashboard (`newrelic_one_dashboard`) com seis widgets: volume diário de OS, tempo
médio por status, latência por endpoint, taxa de erro, CPU/memória dos pods, saúde
dos pods.

Cada condition carrega link de runbook no corpo da notificação; destino por e-mail
via `newrelic_notification_destination` + `newrelic_workflow`.

**Fora de escopo:** `newrelic_service_level` (SLI/SLO como código) e
`newrelic_synthetics_monitor`. SLI/SLO é discutido conceitualmente na RFC-005 sem
recurso Terraform correspondente.

---

## 6. Modelagem do banco

### Diagnóstico

O schema da Fase 2 já é adequadamente indexado: `cliente.documento` unique,
`os.numero` unique, índices em `os.status` e `os.cliente_id`, `veiculo.placa` unique,
`peca.sku` unique, `servico.nome`, `movimentacao (peca_id, criado_em)`, check
constraint em `movimentacao.quantidade`.

Existe, porém, uma lacuna estrutural: `ordem_servico` armazena apenas o status atual
e `atualizado_em`. **Não há registro de quando cada transição ocorreu**, tornando o
dashboard "tempo médio de execução por status" impossível de calcular a partir do
modelo atual.

### Ajuste central — `os.historico_status`

```
id                 uuid PK
ordem_servico_id   uuid FK → os.ordem_servico
status_anterior    varchar(30) NULL      -- NULL na criação
status_novo        varchar(30) NOT NULL
ocorrido_em        timestamptz NOT NULL
duracao_segundos   bigint NULL           -- tempo permanecido no status anterior
usuario_id         uuid NULL

ix_historico_os          (ordem_servico_id, ocorrido_em)
ix_historico_status_data (status_anterior, ocorrido_em)
```

Fonte de verdade do dashboard e origem do custom event `OrdemServicoEvento`. Escrita
na mesma transação da mudança de estado, eliminando divergência entre os dois.

### Ajustes complementares

| Mudança | Motivo |
|---|---|
| Índice composto `(status, criada_em)` em `ordem_servico`, substituindo o índice apenas de `status` | atende o padrão de acesso real (filtrar por status, ordenar por data). `ordem_servico` não possui coluna `atualizado_em`; a coluna de data existente é `criada_em` |
| `ck_item_peca_quantidade_positiva`, `ck_valor_nao_negativo` | paridade de integridade com o que `movimentacao` já possui |
| `ordem_servico.unidade` (varchar, default `'matriz'`) | o cenário do enunciado é múltiplas unidades; sem esta coluna os dashboards não segmentam por oficina |

Aplicados em uma única migration EF Core, executada pelo Job de migração existente.

---

## 7. CI/CD

### Branches

```
feature/* → PR → develop (homologação) → PR → main (produção)
```

Proteção em `main` e `develop` nos quatro repositórios: PR obrigatório, 1 aprovação,
status checks verdes, sem push direto, sem force-push, sem delete. Configuração via
`gh api` em `scripts/setup-branch-protection.sh` (auditável e reproduzível).

### Pipelines

| Repo | CI (PR) | CD |
|---|---|---|
| `oficina-app` | build, test ≥80%, `dotnet format`, docker build, CodeQL | `develop` → namespace `oficina-hml`; `main` → namespace `oficina-prd` |
| `oficina-lambda-auth` | build, testes unitários, `terraform fmt/validate` | `develop` → plan; `main` → publish + zip + `terraform apply` |
| `oficina-infra-k8s` | `fmt -check`, `validate`, `plan` comentado no PR | `main` → `apply` |
| `oficina-infra-db` | idem | `main` → `apply` |

Os repositórios de infraestrutura possuem ambiente único; neles `develop` executa
`plan` e `main` executa `apply`. O deploy automático das duas branches ocorre no
`oficina-app`.

### Roles OIDC

| Role | Assumida por | Permissões |
|---|---|---|
| `oficina-gha-infra` | `infra-k8s`, `infra-db` | VPC, EKS, RDS, IAM, API Gateway |
| `oficina-gha-deploy` | `app` | ECR push, EKS access entry, SSM read |
| `oficina-gha-lambda` | `lambda-auth` | Lambda, API Gateway, SSM read, Secrets read |

Trust policy escopada por branch (`repo:<owner>/<repo>:ref:refs/heads/main` e
`.../develop`). Nenhuma chave estática.

### Bootstrap

`scripts/bootstrap.sh` (versionado em `oficina-infra-k8s`, executado localmente uma
única vez): cria bucket S3 e tabela DynamoDB do tfstate, o OIDC provider do GitHub e
a role `oficina-gha-infra`. A partir daí, tudo é pipeline.

### Secrets e variables

```
oficina-infra-k8s   secrets: AWS_TERRAFORM_ROLE_ARN, NEW_RELIC_LICENSE_KEY,
                             NEW_RELIC_ACCOUNT_ID, NEW_RELIC_API_KEY, JWT_SECRET
oficina-infra-db    secrets: AWS_TERRAFORM_ROLE_ARN, DB_PASSWORD
oficina-lambda-auth secrets: AWS_LAMBDA_ROLE_ARN
oficina-app         secrets: AWS_DEPLOY_ROLE_ARN
                    vars:    AWS_REGION, EKS_CLUSTER_NAME, ECR_REPOSITORY
```

`oficina-app` não recebe mais `JWT_SECRET`, `RDS_ENDPOINT` nem `DB_PASSWORD` como
GitHub Secrets: o CD os obtém do Secrets Manager e do SSM em runtime, usando as
credenciais OIDC.

---

## 8. Documentação arquitetural

Documentos centralizados em `oficina-app/docs/`; os READMEs dos outros três
repositórios apontam para lá. Cada repositório mantém README próprio com seu
diagrama específico.

### RFCs (`docs/rfc/`)

| # | Assunto |
|---|---|
| RFC-001 | Escolha da nuvem: AWS |
| RFC-002 | Escolha do banco gerenciado: RDS PostgreSQL — justificativa formal, ER e relacionamentos |
| RFC-003 | Estratégia de autenticação: CPF → Lambda → JWT → Lambda Authorizer |
| RFC-004 | Topologia de quatro repositórios e contrato via SSM |
| RFC-005 | Plataforma de observabilidade: New Relic |

### ADRs (`docs/arquitetura/`, continuando de ADR-013)

| # | Assunto |
|---|---|
| ADR-014 | API Gateway HTTP API + VPC Link |
| ADR-015 | Lambda Authorizer HS256 em vez do JWT Authorizer nativo |
| ADR-016 | NLB gerenciado pelo Terraform + Service NodePort |
| ADR-017 | Escalabilidade horizontal via HPA |
| ADR-018 | Ambientes por namespace em vez de clusters separados |
| ADR-019 | Duplicação consciente do `Documento.cs` na Lambda |
| ADR-020 | Histórico de status como tabela dedicada |

### Diagramas (Mermaid, versionados)

- Componentes — visão de nuvem com API Gateway, Lambdas, EKS, RDS e New Relic
- Sequência 1 — autenticação por CPF, incluindo os caminhos 400/404/403
- Sequência 2 — abertura de ordem de serviço ponta a ponta
- ER — schema completo com os ajustes da seção 6
- Um diagrama de arquitetura por repositório, no README correspondente

---

## 9. Escopo

### Dentro

- Quatro repositórios com CI/CD, branch protection e READMEs completos
- API Gateway HTTP API com Lambda Authorizer e VPC Link
- Lambda .NET de autenticação por CPF (emissor único de JWT)
- Endpoints de autoatendimento `/api/v1/me/*` e proteção de `/api/v1/consulta/*`
- Migração do Terraform da Fase 2 para os dois repositórios de infraestrutura
- NLB interno gerenciado pelo Terraform e Service NodePort
- New Relic: APM, infraestrutura K8s, logs correlacionados, 5 alertas, 1 dashboard
- Tabela `os.historico_status` e ajustes complementares de schema
- 5 RFCs, 7 ADRs, 4 diagramas centrais e 1 diagrama por repositório
- Vídeo de demonstração de até 15 minutos
- PDF de entrega com links e confirmação do colaborador `soat-architecture`

### Fora

- Lambda de notificações (SNS/SES) — mantido o mock em log da Fase 2
- SLI/SLO como recurso Terraform no New Relic
- Synthetic monitors
- Ambientes com infraestrutura fisicamente separada (hml e prd compartilham cluster e RDS)
- AWS Load Balancer Controller e `TargetGroupBinding`
- Amazon Cognito
- Views materializadas e tuning avançado de queries

---

## 10. Critérios de aceite

1. `POST /auth/cliente` com CPF válido de cliente ativo retorna 200 e JWT utilizável.
2. O mesmo endpoint retorna 400 (CPF inválido), 404 (inexistente) e 403 (inativo) nos
   respectivos cenários.
3. `GET /api/v1/me/ordens-servico` sem token retorna 401; com token de perfil
   `Cliente` retorna apenas as OS daquele cliente.
4. Rotas de gestão retornam 403 para token de perfil `Cliente`.
4a. `GET /api/v1/consulta/{numeroOs}` de uma OS que não pertence ao cliente do token
    retorna 404.
5. Nenhum recurso da aplicação é acessível pela internet fora do API Gateway.
6. Push em `develop` publica em `oficina-hml`; push em `main` publica em `oficina-prd`;
   push direto em ambas é rejeitado pelo GitHub.
7. `terraform apply` executado na ordem documentada, a partir de conta AWS limpa,
   produz o ambiente completo.
8. O dashboard do New Relic exibe volume diário de OS e tempo médio por status com
   dados reais gerados pela aplicação.
9. Um alerta dispara e notifica ao forçar falha no processamento de uma OS.
10. Uma requisição pode ser rastreada ponta a ponta por `correlationId` e por
    `trace.id` no New Relic.
11. Os quatro READMEs contêm propósito, tecnologias, passos de execução/deploy,
    diagrama e link do Swagger.
