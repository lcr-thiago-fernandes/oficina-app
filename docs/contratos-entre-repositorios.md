# Contratos entre repositórios

A Fase 3 divide o sistema em quatro repositórios. Este documento registra o que
**este** repositório (`oficina-app`) fixou e que os outros três precisam honrar.

Cada item aqui é um acoplamento que **falha em silêncio** se divergir: o deploy
funciona, os healthchecks passam, e o sintoma aparece longe da causa. Por isso
estão escritos, e não apenas subentendidos.

| Repositório | Responsabilidade |
|---|---|
| `oficina-app` (este) | API .NET 8 em Kubernetes |
| `oficina-lambda-auth` | Função serverless de autenticação por CPF + Lambda Authorizer |
| `oficina-infra-k8s` | VPC, EKS, ECR, NLB interno, API Gateway, VPC Link, New Relic |
| `oficina-infra-db` | RDS PostgreSQL 16 |

---

## 1. Contrato do token JWT

Fixado dos dois lados: a Lambda **emite**, esta API **valida**. A API não emite
token em nenhum caminho.

```
iss  = oficina-auth
aud  = oficina-api
alg  = HS256
```

Claims: `sub`, `perfil`, `documento`, `nome`, `jti`, `exp`.

| Claim | Regra |
|---|---|
| `perfil` | Exatamente `Cliente`, `Atendente` ou `Admin` (case-sensitive) |
| `documento` | **Obrigatória** no perfil `Cliente`; ausente nos demais |
| `sub` | Presente sempre, mas **informativo** — ver seção 5 |

A política `RequerCliente` exige `perfil=Cliente` **e** a presença de `documento`.
Um token de Cliente sem essa claim recebe 403, não 401.

O segredo de assinatura é compartilhado via AWS Secrets Manager (`oficina/jwt_secret`).
A API valida com o mesmo valor; emissor e validador precisam ler o mesmo segredo.

**Formato do documento:** a API normaliza com `Regex.Replace(entrada, @"\D", "")`,
então tanto faz CPF formatado ou só dígitos — mas os dígitos verificadores
precisam ser válidos, ou `Documento.Criar` rejeita.

---

## 2. Parâmetros do SSM (escritos pela infraestrutura, lidos pelo CD desta API)

O pipeline de CD deste repositório lê estes três caminhos. Os repositórios de
Terraform precisam escrevê-los com **exatamente** estes nomes:

```
/oficina/ecr/repository_url     → URL completa do repositório ECR
/oficina/eks/cluster_name       → nome do cluster EKS
/oficina/db/endpoint            → endpoint (host) do RDS
```

Se um deles não existir, o passo do CD falha com mensagem clara (há `set -euo pipefail`
e atribuição antes do `echo`) — não grava valor vazio.

---

## 3. Segredos do Secrets Manager

```
oficina/db_password             → senha do usuário do banco
oficina/jwt_secret              → segredo HS256, mínimo 32 caracteres
oficina/newrelic_license_key    → license key do New Relic
```

Lidos com `--query SecretString --output text`, ou seja, **texto puro, não JSON**.
Se o Terraform gravar como mapa JSON (padrão do console da AWS), o valor lido será
o JSON inteiro e a autenticação falhará.

**Usuário do banco:** fixado como `oficina_admin` na connection string montada pelo
CD. O Terraform do RDS precisa criar esse usuário com esse nome.

---

## 4. Rede e exposição

| Item | Valor |
|---|---|
| Porta do container | `8080` |
| NodePort — produção | `30080` (namespace `oficina-prd`) |
| NodePort — homologação | `30081` (namespace `oficina-hml`) |
| Health check | `GET /health` |

O Service é `NodePort`, **não** `LoadBalancer`. Quem cria o NLB interno é o
`oficina-infra-k8s`, anexando o auto scaling group dos nós a um target group por
porta. Um Service `LoadBalancer` criaria um segundo balanceador, público, fora do
controle do API Gateway.

O `nodePort` é único no **cluster inteiro**, não por namespace — por isso as duas
portas diferem.

---

## 5. Tabela `auth.usuario` — contrato com `oficina-lambda-auth`

Esta API **escreve** a tabela (bootstrap do admin, com hash BCrypt) mas **não a lê
mais**: o endpoint de login foi removido, e `Usuario.Autenticar` /
`ObterPorUsernameAsync` estão sem consumidor aqui.

A cadeia foi **deliberadamente preservada** porque a Lambda `POST /auth/admin`
autentica o perfil administrativo contra essa tabela. Não remova.

**Decisão registrada:** a autorização por propriedade nesta API usa `documento`,
não `sub`. Uma OS pertence a quem tem o documento correspondente. O `sub` viaja no
token mas não participa de nenhuma decisão de autorização. Se a Lambda emitir um
token com `sub` e `documento` de clientes diferentes, a API honrará o `documento`.

---

## 6. Observabilidade

| Item | Valor |
|---|---|
| Nome da aplicação no APM | `oficina-api` (sem sufixo de ambiente) |
| Separação de ambiente | Label `ambiente:prd` / `ambiente:hml` |
| Evento de negócio | `OrdemServicoEvento` |

Atributos do evento, consumidos pelas consultas dos painéis:
`numeroOs`, `statusAnterior`, `statusNovo`, `duracaoNoStatusSegundos`,
`resultado`, `unidade`.

Valores de `resultado`: `Sucesso` ou `Falha`. Eventos de falha usam
`statusNovo = "NaoAplicavel"`, que fica fora do vocabulário de status para não
contaminar o painel de volume diário.

O agente vem instalado na imagem e **desligado** (`CORECLR_ENABLE_PROFILING=0`);
quem liga é o Deployment.

---

## 6a. Fixado pelo `oficina-lambda-auth` (Plano 2) — para os repositórios de infraestrutura

Detalhes em `oficina-lambda-auth/docs/contratos.md`.

**`oficina-infra-k8s` precisa publicar no SSM, além do que já está na seção 2:**

```
/oficina/apigw/api_id                     → id do HTTP API
/oficina/apigw/vpc_link_integration_id    → id da aws_apigatewayv2_integration (HTTP_PROXY via VPC Link)
/oficina/network/vpc_id
/oficina/network/private_subnet_ids       → StringList (separado por vírgula)
```

A rota protegida `ANY /api/v1/{proxy+}` (com o Lambda Authorizer) é criada pelo
`oficina-lambda-auth`, apontando para essa integração. O `oficina-infra-k8s` **não
deve criar** essa rota: dois `aws_apigatewayv2_route` disputando a mesma `route_key`
em states diferentes dão `ConflictException` no `apply`. O `infra-k8s` cria só as
rotas sem authorizer: `GET /health`, `GET /swagger/{proxy+}` e
`POST /api/v1/ordens-servico/{id}/orcamento/aprovacao`. Motivo: a ordem de apply é
`infra-k8s → infra-db → lambda-auth`; se a rota protegida ficasse no `infra-k8s`, ele
precisaria do `authorizer_id`, que só existe depois.

**Requisito, não sugestão:** o `infra-k8s` também configura throttling no stage
`$default` para `POST /auth/cliente` e `POST /auth/admin` (`route_settings`,
`throttling_rate_limit = 10`, `throttling_burst_limit = 20`). É a única camada
grossa; a proteção fina por IP e por usuário está no `oficina-lambda-auth`
(DynamoDB `oficina-auth-tentativas`).

**`oficina-infra-db` precisa:**

```
/oficina/db/security_group_id             → SG do RDS
```

e declarar as regras desse SG como recursos separados (`aws_vpc_security_group_*_rule`),
nunca inline em `aws_security_group`: o `oficina-lambda-auth` adiciona a regra
`5432 ← sg-lambda-auth` nesse SG, e inline + separado no mesmo SG se apagam a cada apply.

Vale também para o SSM já existente na seção 2: `/oficina/db/endpoint` precisa ser
**só o hostname, sem `:porta`** (`aws_db_instance.address`, **não** `.endpoint`, que
inclui a porta). O CD deste repositório monta `Banco__Host` diretamente com esse
valor; um endpoint com porta embutida quebra a connection string.

**`oficina-lambda-auth` publica:** `/oficina/auth/lambda_authorizer_id`,
`/oficina/auth/api_function_name`.

---

## 7. Pendência que atravessa a fronteira

**Rate limiting / proteção contra brute force no endpoint de autenticação.**

Este repositório tinha rate limit apenas no endpoint de login. Com o login removido,
o subsistema inteiro saiu — e **não existe em lugar nenhum da arquitetura hoje**.

**Resolvido no `oficina-lambda-auth`:** contador de falhas em DynamoDB por IP (10) e por
usuário (5) a cada 15 minutos → 429. O throttling grosso no stage do API Gateway continua
sendo responsabilidade do `oficina-infra-k8s`.

---

## 8. Ordem de provisionamento

```
1. bootstrap (S3 + DynamoDB do tfstate, OIDC provider, role de infra)
2. oficina-infra-k8s     — rede, cluster, ECR, API Gateway, VPC Link
3. oficina-infra-db      — RDS (descobre a VPC por tag)
4. oficina-lambda-auth   — pendura rotas no API Gateway existente
5. oficina-app           — build + deploy no cluster
```

`terraform destroy` roda na ordem inversa.
