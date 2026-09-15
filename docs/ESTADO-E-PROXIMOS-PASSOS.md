# Fase 3 — Estado atual e próximos passos

**Atualizado em:** 2026-09-15
**Para quem retoma o trabalho:** leia este arquivo primeiro. Ele descreve onde o
projeto parou, o que já está decidido, e o que falta fazer.

---

## 1. Onde o projeto está

A Fase 3 divide o sistema em **quatro repositórios**. Um existe e está completo;
três ainda não foram criados.

| # | Repositório | Situação |
|---|---|---|
| 1 | [`oficina-app`](https://github.com/lcr-thiago-fernandes/oficina-app) (este) | ✅ **Completo** — PR #19 merged em `develop` |
| 2 | `oficina-lambda-auth` | ❌ A criar — **próximo passo** |
| 3 | `oficina-infra-k8s` | ❌ A criar |
| 4 | `oficina-infra-db` | ❌ A criar |

Nada foi provisionado na AWS ainda. Nenhuma conta New Relic foi criada. O pipeline
de CD deste repositório existe e está validado sintaticamente, mas **nunca executou
ponta a ponta**, porque a infraestrutura não existe.

### O que este repositório entrega hoje

- API .NET 8 em Clean Architecture, 370 testes, cobertura de linha 89,2%
- **A API não emite JWT** — apenas valida tokens da função serverless (repo 2)
- Perfil `Cliente` por CPF, com endpoints `/api/v1/me/ordens-servico` e `/api/v1/me/veiculos`
- Consulta de orçamento protegida (era anônima, com CPF em query string)
- Tabela `os.historico_status` — fonte do dashboard de tempo médio por status
- Evento de negócio `OrdemServicoEvento` a cada transição da OS
- Correlação de requisições por `X-Correlation-Id`
- Agente APM do New Relic na imagem, versão fixada e hash verificado, desligado por padrão
- Service `NodePort` (para o VPC Link), dois ambientes no mesmo cluster
- CI/CD com segredos vindos do Secrets Manager e do SSM em runtime

### Estado do repositório no GitHub

- **Público** (necessário para branch protection gratuita)
- `main` e `develop` protegidas: PR obrigatório, 1 aprovação, sem force-push, sem delete
- Checks obrigatórios: `build-test` (inclui o gate de 80% de cobertura), `docker-build`, `codeql`
- `soat-architecture` convidado como colaborador — **confirme se ele aceitou**
- `develop` está à frente de `main`. Falta um PR `develop` → `main` quando quiser promover

---

## 2. Documentos que você precisa conhecer

| Arquivo | O que é |
|---|---|
| [`docs/arquitetura/fase3-design-arquitetural.md`](arquitetura/fase3-design-arquitetural.md) | **O design completo dos quatro repositórios.** Decisões, topologia, contratos, custos. É a referência principal para os Planos 2, 3 e 4. |
| [`docs/contratos-entre-repositorios.md`](contratos-entre-repositorios.md) | Os acoplamentos concretos que este repositório fixou e que os outros três precisam honrar. Cada um falha em silêncio se divergir. |
| [`README.md`](../README.md) | Como rodar, como fazer deploy, limitações conhecidas e decisões registradas |

---

## 3. Decisões já tomadas (não reabrir sem motivo)

| Tema | Decisão |
|---|---|
| Nuvem | AWS |
| API Gateway | AWS API Gateway, tipo **HTTP API** (não REST API) |
| Autenticação | Lambda própria emitindo JWT + **Lambda Authorizer** (não o JWT Authorizer nativo, que exigiria OIDC/JWKS com RS256) |
| Runtime da Lambda | .NET 8 |
| Observabilidade | New Relic |
| Acesso da Lambda ao banco | Dentro da VPC, subnet privada |
| Ambientes | 1 cluster EKS, 2 namespaces (`oficina-hml`, `oficina-prd`) |
| Emissor de token | Lambda é o **único** emissor; a API só valida |
| Notificações serverless | **Fora de escopo** (mantido o mock em log) |
| Contrato entre repos | SSM Parameter Store + Secrets Manager (não `terraform_remote_state`) |
| Visibilidade dos repos | Públicos (branch protection em repo privado exige GitHub Pro) |

---

## 4. Próximo passo: Plano 2 — `oficina-lambda-auth`

### O que o repositório precisa conter

- **`oficina-auth-api`** — Minimal API sobre Lambda, **dentro da VPC** (subnet privada).
  Rotas `POST /auth/cliente` (CPF) e `POST /auth/admin` (usuário e senha).
- **`oficina-auth-authorizer`** — Lambda Authorizer, **fora da VPC** de propósito.
  Só valida assinatura HS256; não toca no banco. Lambda em VPC paga ENI e cold start
  maior, e o authorizer é invocado em toda requisição protegida.
- Terraform que pendura as rotas `/auth/*` no API Gateway criado pelo repo 3.
- CI/CD, branch protection, README com diagrama.

### Fluxo de `POST /auth/cliente`

```
1. Documento.Criar(cpf)                  → 400 se dígito verificador inválido
2. SELECT cliente WHERE documento = @cpf → 404 se não existe
                                         → 403 se Ativo = false
3. lê jwt_secret do Secrets Manager (cache estático)
4. assina HS256                          → 200 { access_token, expires_in, perfil }
```

Os três caminhos de erro são exatamente os três verbos do enunciado da fase:
validar o CPF, consultar a existência, consultar o status.

### Contrato do token (fixado, já implementado do lado da API)

```
iss = oficina-auth   ·   aud = oficina-api   ·   alg = HS256
claims: sub, perfil, documento, nome, jti, exp
perfil ∈ { Cliente, Atendente, Admin }
documento presente APENAS no perfil Cliente
```

Segredo compartilhado em `oficina/jwt_secret` (Secrets Manager).

### Pontos de atenção específicos deste plano

1. **Rate limiting é bloqueante de entrega.** Este repositório tinha rate limit apenas
   no endpoint de login; com o login removido, o subsistema inteiro saiu e **não existe
   em lugar nenhum da arquitetura hoje**. A proteção contra brute force precisa nascer
   na Lambda ou no API Gateway.

2. **`auth.usuario` é contrato, não lixo.** Esta API escreve a tabela (bootstrap do
   admin, hash BCrypt) mas não a lê mais. A cadeia `Usuario.Autenticar` /
   `ObterPorUsernameAsync` foi preservada de propósito, porque `POST /auth/admin` vai
   autenticar contra ela. Não remova.

3. **Duplicar `Documento.cs`, não compartilhar pacote.** São ~90 linhas sem dependências
   externas, com validação de dígito verificador. Copie para o repo da Lambda com um
   teste de paridade garantindo que as duas cópias aceitam e rejeitam os mesmos CPFs.
   Compartilhar via NuGet privado acoplaria os repositórios e encadearia os CIs.

4. **Cold start.** `PublishReadyToRun=true`, `MaxPoolSize=2` no Npgsql, segredo cacheado
   em campo `static` fora do handler, memória em 512MB.

---

## 5. Planos 3, 4 e 5 (depois do 2)

**Plano 3 — `oficina-infra-k8s`:** VPC, subnets, NAT, EKS, node group, ECR, roles OIDC,
**NLB interno gerenciado pelo Terraform** (não pelo Kubernetes — o Service é NodePort
justamente por isso), VPC Link, API Gateway HTTP API, metrics-server, New Relic via Helm
(`nri-bundle`), e os dashboards/alertas como código via provider `newrelic`.

**Plano 4 — `oficina-infra-db`:** RDS PostgreSQL 16, subnet group, security group,
parameter group com `log_min_duration_statement`, Performance Insights.
Descobre a VPC por tag (não lê o state do repo 3).

**Plano 5 — Documentação e entrega:** 5 RFCs, 7 ADRs (014 a 020), diagrama de
componentes, dois diagramas de sequência, diagrama ER, vídeo de até 15 minutos, e o
PDF de entrega no Portal do Aluno.

### Ordem de provisionamento (quando a AWS entrar)

```
1. bootstrap (S3 + DynamoDB do tfstate, OIDC provider, role de infra) — script local, uma vez
2. oficina-infra-k8s
3. oficina-infra-db
4. oficina-lambda-auth
5. oficina-app
```

`terraform destroy` na ordem inversa. Custo estimado com tudo no ar: **~US$ 196/mês**
(EKS 73 + 2 nós 60 + NAT 32 + RDS 15 + NLB 16). Derrubar fora das janelas de
demonstração reduz a quase zero.

---

## 6. Pendências conhecidas deste repositório

Nada aqui bloqueia o Plano 2.

| Item | Onde | Observação |
|---|---|---|
| Rate limiting inexistente | arquitetura | **Bloqueante de entrega** — ver seção 4 |
| `ADR-003` desatualizado | `docs/arquitetura/` | Descreve o login removido. Marcar como *superseded*, não editar — Plano 5 |
| `http/auth.http`, `http/demo-video.http` | `http/` | Roteiros da Fase 2, já rotulados como histórico. O roteiro novo é `http/fase3-demo.http` |
| `docs/entrega/*` | `docs/entrega/` | Documentos da Fase 2 |
| HPA usa CPU a 60% | `k8s/hpa.yaml` | Herdado da Fase 2. O ADR de escalabilidade deve documentar **60%**, não 70% |
| `unidade` nunca é populada | domínio | Toda OS nasce `"matriz"`. Documentado no README; alimentar está fora de escopo |
| `format` é advisory no CI | `.github/workflows/ci.yml` | `continue-on-error: true`, por isso não é check obrigatório |
| `ArgumentException` fora do filtro de telemetria | `PublicadorEventoOsExtensions.cs` | Hoje inalcançável de dentro dos delegates instrumentados. Uma linha resolve se virar alcançável |

---

## 7. Como este trabalho foi conduzido

Plano 1 foi executado com implementação e revisão separadas: cada tarefa teve um
executor e, depois, um revisor independente, mais uma revisão ampla da branch no final.
Foram 15 tarefas, 16 revisões e 7 rodadas de correção.

Isso não é cerimônia — três defeitos que teriam quebrado entregáveis avaliados só
apareceram na revisão, e todos passariam pela suíte verde:

- Um índice prescrito sobre uma coluna que não existia
- A coluna `duracao_segundos` nasceria **sempre nula**, porque a leitura não carregava
  o histórico que o cálculo precisa — o dashboard de tempo médio por status viria vazio
- O evento com `statusNovo='Recebida'` **nunca era emitido**, porque a criação da OS não
  fora instrumentada — o dashboard de volume diário viria vazio
- O `catch` da telemetria era irrestrito, então todo erro 422 do cliente disparava o
  alerta Critical obrigatório

Todos falhariam em silêncio, e o sintoma apareceria na gravação do vídeo.

**Recomendação:** mantenha o mesmo rigor nos Planos 2 a 4, sobretudo onde houver
contrato entre repositórios. Foi exatamente nesses vãos que os defeitos se esconderam.
