# Fase 2 — Plano 07: Docker + docker-compose revisados

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:subagent-driven-development. Steps usam checkbox (`- [ ]`).

**Goal:** Revisar o `Dockerfile` (HEALTHCHECK + labels OCI, mantendo multi-stage/non-root) e o `docker-compose.yml` (healthcheck da API + `adminer` opcional para dev), reforçando as boas práticas do módulo de Docker do curso. Sem mudança de código C#.

**Architecture:** O `Dockerfile` já é multi-stage (`sdk:8.0`→`aspnet:8.0`), roda como `USER app` (non-root) e expõe 8080. Adiciona-se um `HEALTHCHECK` (via `curl` ao `/health`, endpoint anônimo já existente) e labels OCI de versão/origem. O `docker-compose.yml` já orquestra `postgres` (healthcheck `pg_isready`) + `api` (envs do `.env`, incl. `Webhook__Token`); adiciona-se healthcheck na `api` e um serviço `adminer` opcional (perfil `tools`) para inspeção do banco na demo.

**Tech Stack:** Docker (multi-stage, Debian bookworm base do `aspnet:8.0`), docker-compose, PostgreSQL 16-alpine.

## Global Constraints

- **Idioma pt-BR** em comentários e mensagens de commit.
- **Sem mudança de código C#**; nenhuma alteração de contrato/rotas. `dotnet build Oficina.sln` deve continuar 0 erros.
- **Non-root preservado** (`USER app`); imagem runtime enxuta (limpar listas do apt; `--no-install-recommends`).
- **Ambiente local sem Docker** → NÃO é possível `docker build`/`compose up` aqui. Validação real: build da imagem no **CI** e `docker compose up` pela pessoa. O gate local desta task é apenas: arquivos bem-formados + `dotnet build` inalterado.
- `.env.example` já contém `WEBHOOK_TOKEN` (Plano 06) — não duplicar.
- **Branch:** `fase-2`. Commit pt-BR terminando com:
  `Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>`

## Estrutura de arquivos

```
docker/Dockerfile             MODIFICADO (+ HEALTHCHECK via curl; + labels OCI; curl instalado minimamente)
docker/docker-compose.yml     MODIFICADO (+ healthcheck da api; + servico adminer opcional em profile "tools")
.dockerignore                 MODIFICADO (+ .superpowers/, **/*.user)
```

---

## Task 1: Revisar Dockerfile e docker-compose

**Files:**
- Modify: `docker/Dockerfile`
- Modify: `docker/docker-compose.yml`
- Modify: `.dockerignore`

- [ ] **Step 1: Substituir `docker/Dockerfile`**

```dockerfile
# syntax=docker/dockerfile:1.7
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["Oficina.sln", "./"]
COPY ["src/Oficina.Api/Oficina.Api.csproj",            "src/Oficina.Api/"]
COPY ["src/Oficina.Aplicacao/Oficina.Aplicacao.csproj", "src/Oficina.Aplicacao/"]
COPY ["src/Oficina.Dominio/Oficina.Dominio.csproj",     "src/Oficina.Dominio/"]
COPY ["src/Oficina.Adaptadores/Oficina.Adaptadores.csproj", "src/Oficina.Adaptadores/"]
COPY ["src/Oficina.Infraestrutura/Oficina.Infraestrutura.csproj", "src/Oficina.Infraestrutura/"]
RUN dotnet restore src/Oficina.Api/Oficina.Api.csproj

COPY src/ src/
RUN dotnet publish src/Oficina.Api/Oficina.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# curl instalado minimamente apenas para o HEALTHCHECK do container (o /health e anonimo).
# Em producao o mecanismo de saude e o probe do Kubernetes; aqui e conveniencia para compose/docker ps.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

ENV ASPNETCORE_URLS=http://+:8080
ENV DOTNET_PRINT_TELEMETRY_MESSAGE=false
EXPOSE 8080

LABEL org.opencontainers.image.title="Oficina Mecanica API" \
      org.opencontainers.image.description="MVP back-end da oficina (FIAP 15SOAT Fase 2)" \
      org.opencontainers.image.source="https://github.com/lcr-thiago-fernandes/fiap_15SOAT_fase1" \
      org.opencontainers.image.licenses="MIT"

HEALTHCHECK --interval=30s --timeout=3s --start-period=20s --retries=3 \
    CMD curl -fsS http://localhost:8080/health || exit 1

USER app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Oficina.Api.dll"]
```

> **Nota (importante):** foi acrescentado o `.csproj` do novo projeto `Oficina.Adaptadores` na etapa de `COPY`/restore (não existia na Fase 1) — sem ele o `dotnet restore` do grafo falharia no build da imagem. Esta é a correção mais crítica deste plano.

- [ ] **Step 2: Substituir `docker/docker-compose.yml`**

```yaml
name: fiap_15SOAT_fase1

services:
  postgres:
    image: postgres:16-alpine
    container_name: oficina-postgres
    environment:
      POSTGRES_DB: oficina
      POSTGRES_USER: ${DB_USER}
      POSTGRES_PASSWORD: ${DB_PASSWORD}
    ports:
      - "5432:5432"
    volumes:
      - oficina_pgdata:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U $${POSTGRES_USER} -d oficina"]
      interval: 5s
      timeout: 5s
      retries: 10
    restart: unless-stopped

  api:
    build:
      context: ..
      dockerfile: docker/Dockerfile
    container_name: oficina-api
    environment:
      ASPNETCORE_ENVIRONMENT: ${ASPNETCORE_ENVIRONMENT:-Development}
      ConnectionStrings__Default: "Host=postgres;Port=5432;Database=oficina;Username=${DB_USER};Password=${DB_PASSWORD}"
      Jwt__Secret: ${JWT_SECRET}
      Jwt__Issuer: ${JWT_ISSUER}
      Jwt__Audience: ${JWT_AUDIENCE}
      AdminBootstrap__Password: ${ADMIN_BOOTSTRAP_PASSWORD}
      Webhook__Token: ${WEBHOOK_TOKEN}
    ports:
      - "8080:8080"
    depends_on:
      postgres:
        condition: service_healthy
    healthcheck:
      test: ["CMD", "curl", "-fsS", "http://localhost:8080/health"]
      interval: 15s
      timeout: 3s
      start_period: 20s
      retries: 5
    restart: unless-stopped

  # Ferramenta opcional para inspecionar o banco na demo: `docker compose --profile tools up adminer`
  adminer:
    image: adminer:4
    container_name: oficina-adminer
    profiles: ["tools"]
    ports:
      - "8081:8080"
    depends_on:
      postgres:
        condition: service_healthy
    restart: unless-stopped

volumes:
  oficina_pgdata:
```

- [ ] **Step 3: Atualizar `.dockerignore` (higiene)**

Adicionar duas linhas ao final de `.dockerignore` (manter o conteúdo existente):
```
**/*.user
.superpowers/
```

- [ ] **Step 4: Sanity — build .NET inalterado**

Run: `dotnet build Oficina.sln`
Expected: **Build succeeded**, 0 erros (nenhum código mudou; garante que o repo continua íntegro).

- [ ] **Step 5: Verificações estáticas dos artefatos Docker**

Run:
```bash
cd "C:/Users/thiago/source/repos/Fiap/fiap_15SOAT_fase1"
# O .csproj do Adaptadores precisa estar no Dockerfile:
grep -n "Oficina.Adaptadores.csproj" docker/Dockerfile
# HEALTHCHECK presente:
grep -n "HEALTHCHECK" docker/Dockerfile
# compose referencia Webhook__Token e adminer em profile tools:
grep -n "Webhook__Token\|profiles" docker/docker-compose.yml
```
Expected: cada grep retorna a(s) linha(s) esperada(s). (Não há `docker` local para `docker build`; a construção real da imagem é validada no CI — Plano 10.)

- [ ] **Step 6: Commit**

```bash
cd "C:/Users/thiago/source/repos/Fiap/fiap_15SOAT_fase1"
git add docker/Dockerfile docker/docker-compose.yml .dockerignore
git commit -F - <<'EOF'
chore(docker): HEALTHCHECK, labels OCI e adminer opcional; inclui Adaptadores no build

Dockerfile: adiciona HEALTHCHECK (curl /health), labels OCI e o .csproj do novo
projeto Oficina.Adaptadores na etapa de restore (sem ele o build da imagem
quebraria). docker-compose: healthcheck da api e servico adminer opcional
(profile "tools"). Non-root e multi-stage preservados. Build da imagem validado
no CI (sem Docker local).

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
EOF
```

---

## Nota de verificação

Sem Docker local, `docker build`/`docker compose up` não rodam aqui. A imagem é construída no **CI** (Plano 10) e executada pela pessoa via `docker compose -f docker/docker-compose.yml --env-file .env up -d --build`. O ponto de maior risco corrigido neste plano — o `.csproj` de `Oficina.Adaptadores` ausente do `Dockerfile` (que quebraria o `dotnet restore` na imagem após o refactor da Fase 2) — é verificável estaticamente (Step 5) e será confirmado no primeiro build de CI.
