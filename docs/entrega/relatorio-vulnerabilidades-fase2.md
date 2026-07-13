# Relatório de análise de vulnerabilidades — Fase 2

**Data do scan:** 2026-07-06 · **Branch:** `main` · **Responsável:** Thiago Fernandes da Cruz

## Ferramentas
- **GitHub Dependabot** (SCA) — ecossistemas `nuget`, `github-actions`, `docker` (`.github/dependabot.yml`), semanal.
- **GitHub CodeQL** (SAST, C#) — no `ci.yml`.
- **`dotnet list package --vulnerable --include-transitive`** — local + CI.

## Resultado (scan de 2026-07-06)

| Escopo | Pacote | Severidade | Advisory | Entra na imagem Docker? |
|---|---|---|---|---|
| `Oficina.Api` (produção) | `OpenTelemetry.Api` 1.9.0 | **Moderada** | [GHSA-g94r-2vxg-569j](https://github.com/advisories/GHSA-g94r-2vxg-569j) | ✅ sim |
| Projetos de teste | `System.Net.Http` 4.3.0 | **Alta** | [GHSA-7jgj-8wvc-jh57](https://github.com/advisories/GHSA-7jgj-8wvc-jh57) | ❌ não |
| Projetos de teste | `System.Text.RegularExpressions` 4.3.0 | **Alta** | [GHSA-cmhx-cq75-c4mj](https://github.com/advisories/GHSA-cmhx-cq75-c4mj) | ❌ não |

As bibliotecas de produção **`Oficina.Dominio`, `Oficina.Aplicacao`, `Oficina.Adaptadores` e `Oficina.Infraestrutura` estão sem vulnerabilidades**.

**Contagem (Dependabot, branch default):** Crítica 0 · Alta **6** (todas transitivas, **apenas em projetos de teste**) · Moderada **1** (produção) · Baixa 0.

## Decisões

### 6 vulnerabilidades Altas — `System.Net.Http` / `System.Text.RegularExpressions` 4.3.0 (só em teste) — **ACEITAS**
Trazidas transitivamente por xUnit/Moq/Testcontainers nos projetos `*.Testes`. **Não entram no contêiner de produção**: o `docker/Dockerfile` é multi-stage e copia apenas o output de `dotnet publish` de `Oficina.Api` sobre `mcr.microsoft.com/dotnet/aspnet:8.0` — os pacotes dos projetos de teste ficam fora da imagem. Risco operacional em produção: **desprezível**. É o mesmo achado já aceito na Fase 1.
- **Mitigação planejada (não aplicada):** atualizar as libs de teste (xUnit/Moq/Coverlet) ou forçar override de `System.Net.Http`/`System.Text.RegularExpressions` (`>= 4.3.4`) via `Directory.Packages.props`.

### 1 vulnerabilidade Moderada — `OpenTelemetry.Api` 1.9.0 (produção) — **ACEITA para o MVP**
Introduzida na Fase 2 pela observabilidade (`/metrics` via OpenTelemetry). O advisory é de **negação de serviço por cardinalidade não-limitada de tags de métrica** em versões antigas do OpenTelemetry. Contexto que reduz o risco no MVP:
- O endpoint `/metrics` **não é exposto publicamente por design** — em produção o acesso é restrito por rede/NetworkPolicy (scraping interno do Prometheus).
- A aplicação **não gera dimensões de métrica a partir de entrada arbitrária do usuário** (usa a instrumentação padrão de ASP.NET Core/runtime), então não há vetor prático de explosão de cardinalidade.
- **Mitigação planejada:** subir o conjunto de pacotes OpenTelemetry (`Api`/`Instrumentation.*`/`Exporter.Prometheus.AspNetCore`) para uma versão corrigida — o **Dependabot** (nuget semanal) já está configurado e abrirá o PR do bump automaticamente; a atualização deve ser coordenada entre os pacotes OTel (o exporter Prometheus está em pré-release `1.9.0-beta.2`).

## OWASP / próximos passos
- **A06 Vulnerable and Outdated Components:** Dependabot (SCA) + CodeQL (SAST) rodando no CI; bump dos pacotes OTel e das libs de teste no próximo ciclo.
- Demais mitigações de segurança da aplicação (JWT, BCrypt, rate limiting, anti-enumeração, webhook com token *fixed-time*/fail-closed, secrets fora do repo/via K8s Secret) estão descritas no README e nos ADRs.
