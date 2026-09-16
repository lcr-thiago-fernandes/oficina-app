# RFC-004 — Topologia de quatro repositórios e contrato via SSM

**Status:** Aceita
**Data:** 2026-09-15
**Autor:** Thiago Fernandes
**ADRs relacionados:** ADR-014, ADR-016, ADR-018

## 1. Contexto e problema

A Fase 2 era um monorepo: aplicação e Terraform de infraestrutura no mesmo
repositório, um único pipeline. A Fase 3 adiciona componentes com ciclos de vida
muito diferentes — a VPC e o cluster EKS mudam raramente (da ordem de uma vez por
semestre), a API muda toda semana, a Lambda de autenticação e o banco ficam entre os
dois extremos. Manter tudo em um pipeline único significa que um `terraform plan` da
rede roda a cada push da API, e que um `apply` de infraestrutura pode nunca ser
isolado de um deploy de aplicação. A pergunta deste RFC é como dividir o sistema em
repositórios de forma que cada ciclo de vida tenha seu próprio raio de explosão, sem
inventar um mecanismo de acoplamento tão frágil quanto o monorepo que ele substitui.

## 2. Alternativas consideradas

| Opção | Raio de explosão de um `apply` | Tempo de CI | Rollback | Custo novo introduzido |
|---|---|---|---|---|
| (a) Monorepo com pipelines por pasta | Todo o repositório continua num state só (ou states acoplados por convenção de pasta); um `apply` de rede compartilha histórico de commits com a aplicação | CI roda em todo push, mesmo quando só a API mudou, a menos que se invista em detecção de path | Reverter um commit pode reverter rede e aplicação juntas | Nenhum contrato explícito — o acoplamento é implícito no monorepo |
| (b) Dois repositórios (app + infra) | Menor que (a), mas um único `apply` de infra ainda mistura VPC, EKS, RDS e Lambda no mesmo state — mudar o banco arrisca o cluster | Melhor que (a) | Rollback de infra ainda é tudo-ou-nada | Um contrato (app ↔ infra), mais simples que quatro, mas ainda concentra risco de state |
| (c) Quatro repositórios (`oficina-app`, `oficina-infra-k8s`, `oficina-infra-db`, `oficina-lambda-auth`) — **escolhida** | Menor: um `apply` do banco não toca o state do cluster nem o da Lambda | CI de cada repositório roda só quando ele muda | Rollback por componente, cada um com seu próprio histórico | Três contratos de fronteira (k8s↔db, k8s↔lambda-auth, infra↔app) que precisam ser escritos e mantidos — o custo novo que esta decisão introduz |

O custo que a opção (c) paga, e que (a) e (b) evitam, é o **contrato entre
repositórios**: identificadores que um lado cria e o outro precisa consumir com o
nome exato. Duas formas foram comparadas para esse contrato:

**`terraform_remote_state` × SSM Parameter Store / Secrets Manager.**
`terraform_remote_state` foi recusado por três razões, registradas nos comentários dos
próprios `backend.tf` dos três repositórios de infraestrutura, todos com a mesma
substância — "nenhum lê o state do outro" — embora só o `backend.tf` de
`oficina-infra-k8s` e o de `oficina-infra-db` citem esta RFC pelo nome entre
parênteses:

1. **Acopla os states.** Ler o state remoto de outro repositório expõe todos os
   atributos de todos os recursos daquele state, não só o identificador necessário —
   uma refatoração interna (renomear um recurso, trocar `for_each` por índice) que não
   muda nenhum valor exposto ainda quebra o `terraform_remote_state` do consumidor,
   porque o bloco de dados inteiro é reavaliado.
2. **Exige permissão de leitura cruzada no bucket.** A role de infraestrutura de um
   repositório precisaria de `s3:GetObject` na chave do state de outro — uma
   concessão de acesso que o SSM Parameter Store dispensa: cada repositório só
   escreve e lê parâmetros nomeados, nunca o state alheio inteiro.
3. **Um `plan` de um repositório falharia por causa de outro.** Se o repositório B
   depende do `terraform_remote_state` do repositório A, um erro de `state lock` ou
   uma versão de state incompatível em A impede até o `plan` (não seria só o `apply`)
   de B — mesmo quando B não mudou nada.

O SSM Parameter Store (para identificadores) e o Secrets Manager (para valores
sensíveis) resolvem os três pontos: cada consumidor lê exatamente o parâmetro
nomeado que precisa, por `data "aws_ssm_parameter"` ou
`data "aws_secretsmanager_secret"`, sem tocar no state do produtor.

## 3. Decisão

Manter os **quatro repositórios** (`oficina-app`, `oficina-infra-k8s`,
`oficina-infra-db`, `oficina-lambda-auth`), cada um com seu próprio backend S3 e sua
própria `key` de state (`infra-k8s/terraform.tfstate`, `infra-db/terraform.tfstate`,
`lambda-auth/terraform.tfstate`, no mesmo bucket `oficina-tfstate-fiap-15soat`), e
formalizar o contrato entre eles por **SSM Parameter Store** (identificadores,
sempre `String` ou `StringList`) e **Secrets Manager** (valores sensíveis), **sempre
em texto puro, nunca em JSON** — um valor gravado como mapa JSON quebra qualquer
consumidor que leia com `--query SecretString --output text` esperando o valor
puro.

### Tabela completa de parâmetros do SSM

| Parâmetro | Tipo | Escreve | Lê |
|---|---|---|---|
| `/oficina/network/vpc_id` | String | `oficina-infra-k8s` (`terraform/ssm.tf`) | `oficina-infra-db` (`terraform/data.tf`), `oficina-lambda-auth` (`terraform/data.tf`) |
| `/oficina/network/private_subnet_ids` | StringList | `oficina-infra-k8s` (`terraform/ssm.tf`) | `oficina-infra-db` (`terraform/data.tf`), `oficina-lambda-auth` (`terraform/data.tf`) |
| `/oficina/network/eks_node_sg_id` | String | `oficina-infra-k8s` (`terraform/ssm.tf`) | `oficina-infra-db` (`terraform/data.tf`) — origem da regra `5432 ← sg-nodes` |
| `/oficina/apigw/api_id` | String | `oficina-infra-k8s` (`terraform/ssm.tf`) | `oficina-lambda-auth` (`terraform/data.tf`) — para pendurar rotas e authorizer |
| `/oficina/apigw/vpc_link_id` | String | `oficina-infra-k8s` (`terraform/ssm.tf`) | Nenhum consumidor em `.tf`; informativo |
| `/oficina/apigw/vpc_link_integration_id` | String | `oficina-infra-k8s` (`terraform/ssm.tf`) | `oficina-lambda-auth` (`terraform/data.tf`) — alvo da rota protegida `ANY /api/v1/{proxy+}` |
| `/oficina/eks/cluster_name` | String | `oficina-infra-k8s` (`terraform/ssm.tf`) | CD do `oficina-app` (`.github/workflows/cd.yml`, `aws eks update-kubeconfig`) |
| `/oficina/ecr/repository_url` | String | `oficina-infra-k8s` (`terraform/ssm.tf`) | CD do `oficina-app` (`.github/workflows/cd.yml`, build/push da imagem) |
| `/oficina/db/endpoint` | String | `oficina-infra-db` (`terraform/ssm.tf`) | CD do `oficina-app` (`.github/workflows/cd.yml`, monta `Host=`), `oficina-lambda-auth` (`terraform/data.tf`, `Banco__Host` da `oficina-auth-api`) |
| `/oficina/db/security_group_id` | String | `oficina-infra-db` (`terraform/ssm.tf`) | `oficina-lambda-auth` (`terraform/data.tf`) — acrescenta a regra `5432 ← sg-lambda-auth` nesse SG |
| `/oficina/auth/lambda_authorizer_id` | String | `oficina-lambda-auth` (`terraform/ssm.tf`) | Nenhum consumidor em `.tf`; informativo |
| `/oficina/auth/api_function_name` | String | `oficina-lambda-auth` (`terraform/ssm.tf`) | Nenhum consumidor em `.tf`; informativo |

### Secrets Manager (sempre texto puro)

| Segredo | Cria | Lê |
|---|---|---|
| `oficina/jwt_secret` | `oficina-infra-k8s` (`terraform/secrets.tf`) | CD do `oficina-app` (Secret do K8s), `oficina-lambda-auth` (`terraform/data.tf` + runtime da `oficina-auth-api`/`oficina-auth-authorizer`) |
| `oficina/db_password` | `oficina-infra-db` (`terraform/senha.tf`, `random_password`) | CD do `oficina-app`, `oficina-lambda-auth` (`terraform/data.tf` + runtime da `oficina-auth-api`) |
| `oficina/newrelic_license_key` | `oficina-infra-k8s` (`terraform/secrets.tf`, sempre criado — D2) | CD do `oficina-app`, `oficina-lambda-auth` (condicional a `newrelic_habilitado`, `terraform/data.tf`) |

### V8 — descoberta pelo SSM, não por tag

O design original (`fase3-design-arquitetural.md`, seção "Dependência circular
resolvida") previa que o `oficina-infra-db` descobrisse o security group dos nós do
EKS **por tag** (`aws:eks:cluster-name = oficina-eks`), via `data source` de busca por
tag, para recriar a regra de ingress `5432 ← sg-nodes` sem depender do state do
`oficina-infra-k8s`. Na implementação, o security group dos nós criado pelo módulo
`terraform-aws-modules/eks/aws` (`oficina-infra-k8s/terraform/eks.tf`) **não carrega**
essa tag da forma que o design supunha. A solução implementada substitui a busca por
tag por publicação explícita: `oficina-infra-k8s` publica
`/oficina/network/eks_node_sg_id` no SSM (`terraform/ssm.tf`), e `oficina-infra-db` o
lê por `data "aws_ssm_parameter"` (`terraform/data.tf`) — o mesmo mecanismo usado para
todo o resto do contrato, sem depender de uma tag que o recurso real não carrega.

### V9 — `/oficina/db/endpoint` é só o hostname

`/oficina/db/endpoint` (`oficina-infra-db/terraform/ssm.tf`) grava
`aws_db_instance.oficina.address` — **não** `aws_db_instance.oficina.endpoint`, que
incluiria a porta no formato `host:porta`. A porta (`5432`) é configurada à parte,
por cada consumidor: o CD do `oficina-app` monta `Host=${RDS_ENDPOINT};Port=5432;...`
(`.github/workflows/cd.yml`), e a `oficina-auth-api` recebe o mesmo hostname puro em
`Banco__Host`. Um `.endpoint` com porta embutida quebraria essas duas connection
strings, que já concatenam `Port=5432` separadamente.

### Ordem de provisionamento

```
1. scripts/bootstrap.sh   (S3 + DynamoDB do tfstate, OIDC provider, role de infra)
2. oficina-infra-k8s      (VPC, EKS, ECR, NLB, API Gateway, VPC Link)
3. oficina-infra-db       (RDS — lê VPC, subnets e SG dos nós do SSM /oficina/network/*)
4. oficina-lambda-auth    (pendura rotas /auth/* e ANY /api/v1/{proxy+} no API Gateway existente)
5. oficina-app            (build da imagem + deploy no cluster)
```

`terraform destroy` roda na **ordem inversa** (5 → 4 → 3 → 2 → 1): a regra de ingress
que o `oficina-lambda-auth` acrescenta no SG do RDS referencia um SG que só existe
enquanto o `oficina-infra-db` não foi destruído, e o `route_settings` de throttling do
`oficina-infra-k8s` (quando habilitado) referencia rotas que só existem enquanto o
`oficina-lambda-auth` não foi destruído.

Consequência direta desta ordem: o CD do `oficina-infra-db` **fica vermelho** (falha
no `data source` do SSM) até o `oficina-infra-k8s` ser aplicado pelo menos uma vez —
comportamento **esperado**, não uma regressão. É o mesmo padrão descrito para o
throttling de `/auth/*` no [ADR-014](../arquitetura/ADR-014-api-gateway-http-api.md):
uma dependência de ordem entre repositórios que só se resolve depois do primeiro
`apply` do repositório anterior na cadeia.

## 4. Consequências

- ✅ CI rápido e raio de explosão pequeno: um `apply` do `oficina-infra-db` não
  reavalia nem arrisca o state do `oficina-infra-k8s` ou do `oficina-lambda-auth`.
- ✅ Os repositórios de infraestrutura podem ser aplicados sem tocar a aplicação: o
  `oficina-app` só lê parâmetros já publicados, nunca aciona `terraform apply` de
  infraestrutura.
- ⚠️ **O contrato falha em silêncio.** Um nome de parâmetro divergente (um `/oficina/db/endpoint`
  escrito como `/oficina/db/host`, por exemplo) passa no `terraform validate` de quem
  escreve — o parâmetro é criado normalmente, só com outro nome — e o sintoma aparece
  longe da causa, no consumidor, geralmente como uma falha de `data source` no `plan`
  ou uma connection string vazia no CD. É por isso que existem os quatro
  `docs/contratos.md` (um por repositório) e o job `contratos` do CI de cada
  repositório de infraestrutura (`oficina-infra-k8s/scripts/verificar-contratos.sh`),
  que confere por grep os nomes que dá para conferir sem aplicar nada.
- ⚠️ A ordem de apply passa a importar de verdade: o CD do `oficina-infra-db` fica
  vermelho até o `oficina-infra-k8s` ser aplicado — comportamento esperado (ver seção
  3), não uma falha a corrigir.
- ⚠️ Mudar um contrato exige PR em pelo menos dois repositórios (quem escreve e quem
  lê), coordenados manualmente — não há um mecanismo que force os dois lados a mudar
  juntos além da revisão humana e dos `contratos.md`.

## 5. Como isto está implementado

| O quê | Onde |
|---|---|
| Documento síntese dos quatro contratos (fonte deste RFC) | `oficina-app/docs/contratos-entre-repositorios.md` |
| Contrato fixado pelo `oficina-lambda-auth` | `oficina-lambda-auth/docs/contratos.md` |
| Contrato fixado pelo `oficina-infra-k8s` | `oficina-infra-k8s/docs/contratos.md` |
| Contrato fixado pelo `oficina-infra-db` | `oficina-infra-db/docs/contratos.md` |
| Script de verificação do contrato no CI | `oficina-infra-k8s/scripts/verificar-contratos.sh` |
| Parâmetros SSM escritos pela rede/cluster/gateway | `oficina-infra-k8s/terraform/ssm.tf` |
| Parâmetros SSM escritos pelo banco (V9) | `oficina-infra-db/terraform/ssm.tf` |
| Parâmetros SSM escritos pela autenticação serverless | `oficina-lambda-auth/terraform/ssm.tf` |
| Descoberta de VPC/subnets/SG dos nós pelo SSM, não por tag (V8) | `oficina-infra-db/terraform/data.tf` |
| Segredos do Secrets Manager (JWT, New Relic) | `oficina-infra-k8s/terraform/secrets.tf` |
| Segredo do Secrets Manager (senha do banco) | `oficina-infra-db/terraform/senha.tf` |
| Backends S3 separados, um por repositório, mesmo bucket | `oficina-infra-k8s/terraform/backend.tf`, `oficina-infra-db/terraform/backend.tf`, `oficina-lambda-auth/terraform/backend.tf` |
| CD do `oficina-app` consumindo o contrato em runtime | `oficina-app/.github/workflows/cd.yml` |
