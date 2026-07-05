# Tech Challenge — Fase 2 (15SOAT) — Design

**Data:** 2026-07-04
**Curso:** Pós-tech FIAP — Arquitetura de Software (15SOAT)
**Entregável:** Evolução do MVP da Fase 1 (back-end da oficina) para qualidade, resiliência e escalabilidade, com refatoração em Clean Architecture (estilo do curso) e infraestrutura moderna (Docker, Kubernetes/EKS, Terraform/AWS, CI/CD).
**Base:** mesmo repositório da Fase 1 (`github.com/lcr-thiago-fernandes/fiap_15SOAT_fase1`). Vale 60% da nota da fase.

---

## 1. Visão geral

Com o aumento de demanda e a expansão da oficina, a aplicação da Fase 1 precisa evoluir para: reduzir riscos operacionais com infraestrutura escalável, automatizar provisionamento e deploy, melhorar qualidade/organização do código e suportar picos de ordens de serviço com escalabilidade dinâmica.

Esta fase **não reescreve** o domínio: preserva as regras de negócio da Fase 1 (máquina de estados da OS, invariantes de estoque, snapshot de preço, anti-enumeração) e **reorganiza o código** na Clean Architecture ensinada no curso, **altera/cria** um conjunto de APIs e **adiciona toda a camada de infraestrutura como código + CI/CD + orquestração**.

### Princípios
- **Clean Architecture (estilo do curso, Erick Muller)**: 4 camadas (Entidades → Casos de Uso → Adaptadores de Interface → Frameworks e Drivers) com regra de dependência por inversão/DI.
- **Clean Code**: nomes claros, responsabilidade única, simplicidade; aproveitar o refactor para remover código morto da Fase 1.
- **YAGNI**: observabilidade mínima (só métricas + logs), e-mail em mock/log, sem multi-região, sem ECS (o requisito é Kubernetes), sem service mesh.
- **AWS-orientado**: alinhado às aulas (Terraform AWS, ECS, registry). Cluster e banco **provisionados sob demanda** e destruídos após a demo (controle de custo).
- **pt-BR** em código, identificadores, comentários e commits (convenção do projeto).

---

## 2. Decisões (fechadas no brainstorming)

| # | Decisão | Escolha |
|---|---|---|
| 1 | Padrão de arquitetura | **Clean Architecture estilo curso** (restruturação completa; não Hexagonal, não refactor leve) |
| 2 | Nº de projetos .NET | **5 projetos** (separando `Oficina.Adaptadores`) |
| 3 | Wiring dos blocos | **DI idiomático** do .NET (classes stateless via container), preservando a estrutura/nomenclatura do curso (não `new`/`static` manual) |
| 4 | Cloud / orquestração | **AWS: EKS + RDS** provisionados via Terraform |
| 5 | Ciclo de vida do cluster | **Sob demanda**: `terraform apply` quando necessário, `destroy` após gravar o vídeo; CI/CD faz build/test/imagem/deploy no cluster existente |
| 6 | Observabilidade | **Mínima**: `/metrics` Prometheus (OpenTelemetry .NET) + logs Serilog em stdout |
| 7 | Notificação de status | **Mock/log** (porta `INotificacaoGateway`; sem SMTP real) |
| 8 | Abertura de OS | Endpoint **consolidado** com semântica **find-or-create** de cliente/veículo |
| 9 | Aprovação de orçamento | **Webhook com token** (Secret) substitui o approve/reject público; consulta pública fica **só-leitura** |
| 10 | Listagem | Ordena por prioridade de status + mais antigas primeiro; **exclui (lógico) Finalizada, Entregue e Cancelada** |
| 11 | Manifestos K8s | **YAML puro** em `/k8s` |
| 12 | Exposição do Service | **`type: LoadBalancer`** (ELB da EKS) |
| 13 | Migrações do banco | **Job de migração** na pipeline antes do rollout; remover migrate-no-boot |
| 14 | State do Terraform | **S3 + DynamoDB** (lock) |
| 15 | Auth do CI/CD na AWS | **OIDC** (sem chave estática); runners **GitHub-hosted** |
| 16 | Repositório | Mesmo repo; branch **`fase-2`** → PR → `main`; tag `v2.0.0` ao final |

---

## 3. Arquitetura de código (Clean Architecture estilo curso)

### 3.1 Camadas e regra de dependência
Modelo de 4 círculos (dentro → fora), conforme o módulo do curso:
1. **Entidades** — regra de negócio, validação interna; **não** 1:1 com tabela; não anêmicas.
2. **Casos de Uso** — 1 por classe, responsabilidade única; recebem **Gateways por DI**; instanciam entidades; podem chamar outros casos de uso; **sem** banco/HTTP.
3. **Adaptadores de Interface** — **Controllers** (orquestrador "mensageiro"), **Gateways** (implementam a abstração de acesso a dados consumida pelo use case; expõem métodos em linguagem de negócio; escondem a origem de dados), **Presenters** (formatam a saída p/ o cliente).
4. **Frameworks e Drivers** — rotas/servidor HTTP, auth, ORM/EF Core, repositórios, integrações externas.

**Regra de dependência:** o interno não acessa o externo diretamente — define uma **interface** e recebe o externo via **DI**. Componentes da mesma camada podem se chamar. Só o externo instancia o interno. Banco e protocolo são "detalhes" trocáveis.

### 3.2 Estrutura de projetos (.NET 8) — 5 projetos

```
Oficina.Dominio        [Entidades]            entidades + VOs (OrdemDeServico, Cliente, Veiculo, Peca, Servico,
        ▲                                      Documento, Placa, ...); validação interna; SEM EF Core
Oficina.Aplicacao      [Casos de Uso]         1 use case por classe; dependem de IXxxGateway (definidas aqui);
        ▲                                      instanciam entidades; orquestração fina só quando é "mesma camada"
Oficina.Adaptadores    [Adaptadores]  ★NOVO   Controllers de aplicação (orquestram vários use cases);
        ▲                                      Gateways (implementam IXxxGateway usando IXxxDataSource);
        ▲                                      Presenters (formatam saída); interfaces IXxxDataSource
Oficina.Infraestrutura [Frameworks&Drivers]   DataSources: OficinaDbContext/EF Core + repos Npgsql implementando
        ▲                                      IXxxDataSource; BCrypt; emissão de JWT; INotificacaoGateway (mock/log)
Oficina.Api            [Frameworks&Drivers]    Controllers HTTP FINOS; middleware; auth/rate-limit; Swagger; DI;
                                               /health; /metrics
```

Referências (fluxo de dependência para dentro): `Api → Adaptadores → Aplicacao → Dominio`; `Infraestrutura → Adaptadores` (implementa `IXxxDataSource`) `→ Aplicacao → Dominio`; `Api → Infraestrutura` (só para wiring de DI).

### 3.3 Fluxo de uma requisição
HTTP Controller (fino, `Api`) → **Controller de aplicação** (`Adaptadores`, orquestra) → **Use Case** (`Aplicacao`; recebe `IXxxGateway` por DI, instancia entidades, aplica regra) → **Gateway** (`Adaptadores`; implementa `IXxxGateway` via `IXxxDataSource`) → **DataSource** (`Infraestrutura`; EF Core) → **Presenter** (`Adaptadores`; formata) → resposta HTTP.

### 3.4 Mapeamento Fase 1 → Fase 2
- Entidades/VOs: **permanecem** em `Oficina.Dominio`.
- Use cases da Fase 1: **permanecem** em `Oficina.Aplicacao`, mas passam a depender de **`IXxxGateway`** (linguagem de negócio) em vez de `IXxxRepositorio` direto.
- **Novos** em `Oficina.Adaptadores`: `Controllers` de aplicação, `Gateways`, `Presenters`, interfaces `IXxxDataSource`.
- Repositórios EF Core da Fase 1: viram **DataSources** em `Oficina.Infraestrutura`, escondidos atrás dos Gateways.
- Controllers HTTP (`Oficina.Api`): ficam **finos** (desserializam, chamam Controller de aplicação, serializam o resultado do Presenter).

### 3.5 Limpezas de Clean Code aproveitadas no refactor
- **Remover os Domain Events mortos** da Fase 1 (records `EventoOs`/`ExecucaoIniciadaEvent` etc. que só eram criados e descartados via `LimparEventos()`); a notificação passa pela porta `INotificacaoGateway`.
- Mover exceções que hoje moram em arquivos errados (ex.: `OrdemInvalidaException` dentro de `CriarOrdemUseCase.cs`; eventos dentro de `ExcecoesOrdemServico.cs`) para locais coerentes.
- Corrigir 2 gaps de segurança baratos: o **500 que vaza stack/tipos** no `MiddlewareDeExcecoes` (só detalhar em Development) e a **ausência de headers de segurança** (X-Content-Type-Options, X-Frame-Options, HSTS em prod). Alinhar o guard do `JWT_SECRET` (código exige ≥32; padronizar em ≥64).

---

## 4. Mudanças de API e domínio

Convenções mantidas da Fase 1: `/api/v1`, ProblemDetails, JWT nas rotas administrativas, Swagger em Development.

### 4.1 Abertura de OS (consolidada) — `POST /api/v1/ordens-servico` (JWT)
Recebe cliente + veículo + serviços + peças e retorna a identificação única.
```
{ cliente:{documento,nome,email,telefone}, veiculo:{placa,marca,modelo,ano},
  servicos:[{servicoId,quantidade}], pecas:[{pecaId,quantidade}] }
→ 201 { id, numero, status:"Recebida", totalServicos, totalPecas, totalGeral, itens... }
```
- **Controller de aplicação `AbrirOrdemDeServicoController`** orquestra os casos de uso: *find-or-create* cliente (por `documento`), *find-or-add* veículo (por `placa` sob o cliente), `AbrirOrdem`, `AdicionarItens` — **tudo em uma transação**.
- Serviços/peças são **referenciados** do catálogo (devem existir; snapshot de nome/preço no momento da inclusão, como na Fase 1). Peça inativa/serviço inativo → 422.
- CRUDs e endpoints de item granulares **permanecem** (necessários para editar a OS antes da execução).

### 4.2 Consulta de status — `GET /api/v1/consulta/{numero}?documento=` (público)
Mantém a rota pública da Fase 1 (número + documento) devolvendo status atual + itens. **Anti-enumeração preservada** (404 idêntico para OS inexistente e documento que não confere). Passa a ser **somente leitura** (ver 4.3).

### 4.3 Aprovação de orçamento (webhook externo) — `POST /api/v1/ordens-servico/{id}/orcamento/aprovacao`
Recebe **notificação externa** (máquina-a-máquina) de aprovação/recusa.
```
Header: X-Webhook-Token: <token em K8s Secret>
{ decisao: "aprovado" | "recusado" }
→ 200 | 401 (token inválido) | 404 | 422 (estado errado)
```
- Reusa `OrdemDeServico.Aprovar()` / `Rejeitar()` do domínio.
- Autenticado por **token compartilhado** guardado em Secret (requisito "tokens de serviços externos"); **não** usa JWT nem documento do cliente.
- **Substitui** os endpoints públicos de approve/reject por documento da Fase 1.

### 4.4 Listagem de OS — `GET /api/v1/ordens-servico` (JWT)
- **Ordenação:** prioridade de status **Em Execução(1) > Aguardando Aprovação(2) > Em Diagnóstico(3) > Recebida(4)**, depois **mais antigas primeiro** (`CriadaEm` asc).
- **Exclusão lógica** (via filtro na query, não soft-delete do registro): oculta **Finalizada, Entregue e Cancelada** (estados terminais).
- Implementado na query do **DataSource/Gateway** (ex.: `CASE` de prioridade + `CriadaEm asc`, `WHERE status IN (ativos)`).

### 4.5 Notificação de status por e-mail (mock)
- Porta `INotificacaoGateway.NotificarMudancaDeStatus(ordem)` consumida pelos casos de uso de transição (após persistir).
- Implementação **mock em `Infraestrutura`** loga (Serilog) `"e-mail enviado p/ {clienteEmail}: OS #{numero} agora está {status}"`. Sem SMTP real (YAGNI); a abstração permite plugar SES/SMTP no futuro.

---

## 5. Containerização (Docker)

- **Dockerfile** (evolui o multi-stage da Fase 1): base `sdk:8.0` → `aspnet:8.0`; `USER app` (non-root, já presente); `EXPOSE 8080`; adicionar **`HEALTHCHECK`** (`/health`), labels de versão; compatível com `readOnlyRootFilesystem` no K8s. `.dockerignore` revisado.
- **docker-compose (dev local)**: serviços `api` (build local), `postgres:16-alpine` (healthcheck + volume nomeado), opcional `adminer`. Sem MailHog (e-mail é mock/log). Variáveis do `.env` (como na Fase 1).

---

## 6. Kubernetes (`/k8s`) — manifestos YAML

Namespace `oficina`. Recursos:
- **Deployment `oficina-api`**: imagem do ECR; `envFrom` ConfigMap + Secret; **resources requests/limits** (pré-requisito do HPA); **liveness/readiness** em `/health`; `securityContext` non-root/read-only; `replicas: 2`.
- **Service** `type: LoadBalancer` (EKS provisiona ELB para acesso externo).
- **ConfigMap** (não-sensível): `ASPNETCORE_ENVIRONMENT`, JWT issuer/audience, host/porta do RDS, flags.
- **Secret** (sensível): senha do DB, `JWT_SECRET`, `ADMIN_BOOTSTRAP_PASSWORD`, `WEBHOOK_TOKEN`. Criado no deploy a partir dos GitHub Secrets (ou AWS Secrets Manager).
- **HPA**: escala por CPU (e memória), `min 2 / max 10`, alvo ~60%. Requer **metrics-server** (instalado via Terraform/helm).
- **Job de migração**: roda `dotnet ef database update` (ou `--idempotent` script) contra o RDS **antes** do rollout; a app **não** migra mais no startup.

*(Kustomize é possível como evolução futura; nesta fase, YAML puro.)*

---

## 7. IaC — Terraform (`/infra`) — AWS

- **Backend remoto**: **S3** (state) + **DynamoDB** (lock).
- **Providers**: `aws`, `kubernetes`, `helm`.
- **Recursos/módulos**:
  - **VPC** enxuta (subnets públicas/privadas; NAT único ou ausente p/ custo).
  - **EKS** (control plane + managed node group pequeno, `t3.small`/spot).
  - **RDS PostgreSQL** `db.t3.micro` (free-tier), subnet privada, SG liberando os nodes da EKS.
  - **ECR** (repositório da imagem).
  - **IAM/OIDC** para o GitHub Actions assumir role sem chave estática; roles de node/IRSA conforme necessário.
  - **metrics-server** via `helm` (para o HPA).
  - **Outputs**: endpoint do cluster, dados de kubeconfig, URL do ECR, endpoint do RDS.
- **`/infra/README.md`**: recursos criados e `terraform init/plan/apply/destroy`.
- **Custo**: recursos pequenos; `terraform destroy` após a demo.

---

## 8. CI/CD — GitHub Actions

Autenticação na AWS via **OIDC** (sem secret estático); runners **GitHub-hosted**. (Self-hosted runners são ensinados no módulo, mas não são necessários aqui.)
- **`ci.yml`** (evolui o da Fase 1): `build → testes (unit + integração Testcontainers) → gate cobertura 80% → qualidade (dotnet format + CodeQL)`. Dispara em PR/push.
- **`cd.yml`** (novo): `build + push da imagem p/ ECR → Job de migração no RDS → kubectl apply /k8s + set image → aguardar rollout`. Dispara no merge para `main` (ou manual).
- **`infra.yml`** (novo, `workflow_dispatch` manual): `terraform apply` / `destroy` — liga/desliga cluster + RDS sob demanda.
- App-secrets (JWT, webhook, DB, admin) vêm de **GitHub Secrets** e são injetados no K8s Secret no deploy.

---

## 9. Observabilidade (mínima)

- **`/metrics`** Prometheus via **OpenTelemetry .NET** (instrumentação ASP.NET Core: taxa/latência de request + métricas de runtime). Encosta no módulo 8 sem subir Jaeger/Grafana/Loki.
- **Serilog** JSON em **stdout** (coletável pelos logs do K8s). Manter o mascaramento de campos sensíveis.
- **`/health`** para liveness/readiness. O HPA usa o metrics-server (CPU/mem); o `/metrics` é para visibilidade.

---

## 10. Testes (fluxos críticos)

Quatro projetos, acompanhando os 5 de produção:
- **`Oficina.Dominio.Testes`** — praticamente intacto (entidades/VOs/máquina de estados).
- **`Oficina.Aplicacao.Testes`** — casos de uso com **mock de `IXxxGateway`** (antes `IXxxRepositorio`).
- **`Oficina.Adaptadores.Testes`** (novo) — Controllers de aplicação (orquestração; ex.: Abertura de OS chamando os use cases na ordem), Gateways (mapeamento entidade↔DataSource com mock), Presenters (formatação).
- **`Oficina.Integracao.Testes`** (Testcontainers) — E2E dos novos fluxos: abertura consolidada; webhook (token válido→200 / inválido→401); **listagem ordenada** (prioridade + antigas + exclusão de terminais); consulta de status; assert da **notificação mock** disparada na transição.
- **Gate de cobertura 80%** mantido no CI; `coverlet.runsettings` ajustado às novas pastas.

---

## 11. Entregáveis

- **README** reescrito: descrição/objetivos da Fase 2; **diagramas Mermaid** — (1) camadas Clean Architecture + componentes, (2) infra AWS (VPC/EKS/RDS/ECR/ELB), (3) fluxo de deploy CI/CD; instruções de **execução local**, **deploy K8s** e **provisionamento Terraform**; link da **collection** (Swagger + Postman exportada); link do **vídeo**.
- **Vídeo ≤15min** (roteiro em `docs/entrega/`): deploy da aplicação, execução do CI/CD, consumo das APIs, e **escalabilidade automática** (gerar carga com `hey`/`k6` ou muitas OS → HPA escala).
- **PDF de entrega** (padrão da Fase 1): link do repo (compartilhado com `soat-architecture`), diagrama de arquitetura e link do vídeo.
- **ADRs novos** em `docs/arquitetura/`: refactor Clean Architecture; AWS/EKS+RDS; webhook de aprovação; migração via Job; observabilidade mínima. **CLAUDE.md atualizado** (estrutura de 5 projetos + comandos de infra: kubectl/terraform/docker).

---

## 12. Estratégia de repositório

- Mesmo repo. Branch **`fase-2`** com PRs para `main` (CI roda no PR; CD dispara no merge). Tag **`v2.0.0`** ao final. Commits em pt-BR.

---

## 13. Fora de escopo (YAGNI, deliberado)

- Stack completa de observabilidade (Jaeger/Grafana/Loki) — só `/metrics` + logs.
- E-mail/SMTP real — mock/log.
- ECS (o requisito é Kubernetes; ECS é só aula).
- Multi-região, service mesh, blue-green/canary, refresh token/SSO, frontend.
- Autoscaling por métricas customizadas (KEDA) — HPA por CPU/memória basta.

---

## 14. Riscos e mitigações

| Risco | Mitigação |
|---|---|
| Custo AWS (EKS/RDS) | Recursos pequenos/spot, `terraform destroy` após a demo, workflow `infra.yml` manual |
| Migração concorrente entre réplicas | Job de migração dedicado antes do rollout; remover migrate-no-boot |
| Refactor quebrar comportamento da Fase 1 | Manter a suíte de testes verde a cada passo (TDD); testes de integração cobrindo os fluxos críticos |
| Regra de dependência violada no refactor | Revisar referências entre projetos; testes de arquitetura opcionais |
| Segredos vazando no CI | OIDC (sem chave estática); GitHub Secrets → K8s Secret; nunca logar segredo |
| HPA não escala (sem métricas) | Instalar metrics-server via Terraform; requests/limits no Deployment |

---

## 15. Decomposição prevista em planos

Uma spec, decomposta em planos sequenciados (estilo Fase 1):
1. **Refactor Clean Architecture** (5 projetos; Controllers/Gateways/Presenters/DataSources; migrar use cases para Gateways; manter testes verdes).
2. **Mudanças de API + notificação** (abertura consolidada, webhook de aprovação, listagem ordenada, consulta de status, notificação mock; remover Domain Events mortos).
3. **Docker + docker-compose** revisados.
4. **Kubernetes** (`/k8s`: Deployment/Service/ConfigMap/Secret/HPA/Job de migração).
5. **Terraform** (`/infra`: VPC/EKS/RDS/ECR/IAM-OIDC/metrics-server; backend S3+DynamoDB).
6. **CI/CD** (`ci.yml`/`cd.yml`/`infra.yml`; OIDC/ECR).
7. **Observabilidade mínima** (`/metrics` OpenTelemetry; ajustes Serilog).
8. **Docs e entrega** (README + diagramas Mermaid, ADRs, collection, roteiro de vídeo, PDF, CLAUDE.md).
