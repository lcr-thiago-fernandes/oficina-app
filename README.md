# Oficina Mecânica — Sistema Integrado de Atendimento e Execução de Serviços

> **Tech Challenge — Fase 1 — Pós-Tech FIAP (15SOAT)**

MVP do back-end de uma oficina mecânica de médio porte. O sistema unifica gestão de clientes, veículos, catálogo de serviços, controle de estoque e o ciclo completo da Ordem de Serviço (OS) — da entrada do veículo à entrega.

---

## Funcionalidades

- ✅ **Gestão de Clientes** (PF/PJ) e seus **veículos**
- ✅ **Catálogo de serviços** com preço base e tempo estimado
- ✅ **Controle de estoque** de peças com saldo, entradas e saídas
- ✅ **Ordem de Serviço** com máquina de estados (Recebida → Diagnóstico → Aprovação → Execução → Finalizada → Entregue)
- ✅ **Aprovação/rejeição de orçamento pelo cliente** (rota pública sem JWT)
- ✅ **Baixa automática de estoque** ao iniciar a execução (transacional)
- ✅ **Métricas administrativas** — tempo médio de execução
- ✅ **Autenticação JWT** para usuários administrativos (perfis Admin/Atendente)
- ✅ **Validação de CPF/CNPJ e placa** (formatos antigo e Mercosul)
- ✅ **Documentação Swagger** acessível em `/swagger`

---

## Stack tecnológica

| Camada | Tecnologia |
|---|---|
| Linguagem | C# 12 / .NET 8 |
| Web | ASP.NET Core (controllers + minimal hosting) |
| ORM | Entity Framework Core 8 + Npgsql |
| Banco | PostgreSQL 16 |
| Auth | JWT HS256 + BCrypt + RateLimiting nativo |
| Validação | FluentValidation |
| Logs | Serilog (Console JSON estruturado) |
| Testes | xUnit + FluentAssertions + Moq + Coverlet + Testcontainers |
| Container | Docker multi-stage + docker-compose |
| CI | GitHub Actions + CodeQL + Dependabot |

### Por que PostgreSQL?

Domínio fortemente relacional (cliente↔veículos↔OS↔itens↔peças), transações ACID essenciais para integridade de orçamento e estoque, schemas separados reforçando bounded contexts, suporte nativo a `BIGSERIAL` para o número humano-amigável da OS, gratuito e maduro. 

---

## Arquitetura

**Clean Architecture** com 4 projetos:

```
Api  ──►  Aplicacao  ──►  Dominio  ◄──  Infraestrutura
                                  ▲
                                  └────  (Api também → Infraestrutura, só p/ DI)
```

`Dominio` é puro (sem dependências externas). `Infraestrutura` implementa as interfaces declaradas no `Dominio`. Diagramas detalhados em [docs/entrega/2.2 - Diagramas conforme apresentado na disciplina de DDD - Contextos Delimitados.md](docs/entrega/2.2 - Diagramas conforme apresentado na disciplina de DDD - Contextos Delimitados.md).

### Bounded Contexts

| Contexto | Tipo | Agregado raiz |
|---|---|---|
| Gestão de Clientes | Suporte | `Cliente` (com `Veiculo`) |
| Catálogo de Serviços | Suporte | `Servico` |
| Estoque | Suporte | `Peca` (com `MovimentacaoEstoque`) |
| Ordem de Serviço | **Núcleo** | `OrdemDeServico` (com `ItemServico`, `ItemPeca`) |

---

## Como rodar localmente

### Pré-requisitos

- Docker + docker compose
- (Opcional) .NET 8 SDK para rodar fora do container

### 1. Clonar e configurar

```bash
git clone https://github.com/lcr-thiago-fernandes/fiap_15SOAT_fase1
cd oficina-mecanica
cp .env.example .env
```

### 2. Subir o ambiente

```bash
docker compose -f docker/docker-compose.yml --env-file .env up -d --build
```

A primeira execução:
- Aplica todas as migrations no Postgres
- Cria o usuário `admin` com a senha de `ADMIN_BOOTSTRAP_PASSWORD` (precisa trocar no primeiro login)

### 3. Acessar

- **API**: http://localhost:8080
- **Swagger**: http://localhost:8080/swagger
- **Health**: http://localhost:8080/health

### 4. Login

```bash
curl -X POST http://localhost:8080/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"<sua-senha>"}'
```

Use o `accessToken` retornado no header `Authorization: Bearer ...` nas demais chamadas.

### Cenários `.http`

A pasta `http/` tem cenários executáveis pelas extensões REST Client (VS Code), Visual Studio ou Rider:

- `auth.http` — login, rate limit
- `clientes.http` — CRUD cliente + veículos
- `servicos.http` — CRUD catálogo
- `pecas.http` — CRUD peça + movimentações
- `ordens-servico.http` — fluxo completo da OS
- `consulta.http` — consulta pública (cliente)

Configure variáveis em `http/_env.http` (host, token, IDs reutilizáveis).

---

## Como rodar testes

```bash
# Todos os testes (unit + integração)
dotnet test

# Com cobertura
dotnet test --collect:"XPlat Code Coverage" --settings coverlet.runsettings

# Gerar relatório HTML
dotnet tool install -g dotnet-reportgenerator-globaltool
reportgenerator -reports:"**/TestResults/**/coverage.cobertura.xml" \
                -targetdir:"TestResults/CoverageReport" \
                -reporttypes:"HtmlInline;TextSummary"
# Abrir TestResults/CoverageReport/index.html
```

Os testes de integração usam **Testcontainers** — sobem um Postgres real efêmero, sem mocks. Requerem Docker rodando.

**Cobertura mínima exigida pelo CI:** 80% (configurado em `.github/workflows/ci.yml`).

---

## Estrutura de pastas

```
oficina-mecanica/
├── .github/             # workflows CI + Dependabot
├── docker/              # Dockerfile + docker-compose
├── docs/
│   ├── entrega/         # entregáveis do Tech Challenge
├── http/                # cenários REST Client
├── src/
│   ├── Oficina.Api/
│   ├── Oficina.Aplicacao/
│   ├── Oficina.Dominio/
│   └── Oficina.Infraestrutura/
└── tests/
    ├── Oficina.Dominio.Testes/
    ├── Oficina.Aplicacao.Testes/
    └── Oficina.Integracao.Testes/
```

---

## Segurança

- Senhas armazenadas com BCrypt cost 12
- JWT HS256, expiração 60 min, sem refresh token (MVP)
- Rate limit 5 tentativas / 15 min por IP no login
- Validação de CPF/CNPJ (dígitos verificadores), placa (antigo + Mercosul), e-mail
- Saldo de peça nunca pode ficar negativo (invariante + check constraint SQL)
- Anti-enumeração na consulta pública (404 idêntico para OS inexistente e documento não conferente)
- Headers seguros, Swagger só em dev
- CodeQL + Dependabot rodando no CI ([relatório](docs/entrega/4 - Relatório com análise de vulnerabilidades.md))

---

## Licença

Projeto acadêmico — uso restrito ao Tech Challenge da FIAP.
