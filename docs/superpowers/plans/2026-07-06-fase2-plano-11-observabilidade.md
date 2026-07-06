# Fase 2 — Plano 11: Observabilidade mínima (OpenTelemetry `/metrics`)

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:subagent-driven-development. Steps usam checkbox (`- [ ]`).

**Goal:** Instrumentar a API com **OpenTelemetry** expondo um endpoint **`/metrics`** no formato Prometheus (métricas de request do ASP.NET Core + métricas de runtime .NET), mantendo o **Serilog** (logs JSON em stdout) e o `/health`. Escopo mínimo alinhado ao módulo 8 — SEM subir Jaeger/Grafana/Loki.

**Architecture:** `AddOpenTelemetry().WithMetrics(...)` registra instrumentação de ASP.NET Core + runtime e o **Prometheus exporter**; `app.MapPrometheusScrapingEndpoint()` publica `/metrics` (anônimo, para o Prometheus/coletor raspar). Serilog e o pipeline atuais não mudam. É testável **sem Docker** reaproveitando o padrão `WebApplicationFactory` que remove o `BootstrapAdminHostedService` (não exige Postgres).

**Tech Stack:** C# 12 / .NET 8, OpenTelemetry .NET (Extensions.Hosting, Instrumentation.AspNetCore, Instrumentation.Runtime, Exporter.Prometheus.AspNetCore).

## Global Constraints

- **Idioma pt-BR** em código/comentários/commit.
- **Não quebrar** contrato HTTP, `/health`, Serilog, nem o modo `migrate` do `Program.cs`. `public partial class Program` preservado.
- `/metrics` **anônimo** (o coletor Prometheus raspa; em produção restringe-se por rede/NetworkPolicy — fora do escopo MVP).
- **Docker indisponível local** → o teste de `/metrics` usa uma factory que **remove o hosted service** (não precisa de Postgres) e roda via **`--filter`** (não a suíte de integração inteira). Gate local: `dotnet build` 0 erros + testes unitários (Domínio/Aplicação/Adaptadores/Infraestrutura) verdes + o teste `/metrics` filtrado verde.
- **Branch:** `fase-2`. Commit pt-BR terminando com:
  `Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>`

## Estrutura de arquivos

```
src/Oficina.Api/Oficina.Api.csproj                          MOD  (+ 4 pacotes OpenTelemetry)
src/Oficina.Api/Program.cs                                  MOD  (+ AddOpenTelemetry().WithMetrics; + MapPrometheusScrapingEndpoint)
tests/Oficina.Integracao.Testes/MetricasPrometheusTestes.cs  NOVO (factory sem Docker; GET /metrics → 200 + formato Prometheus)
```

---

## Task 1: Expor `/metrics` via OpenTelemetry

**Files:**
- Modify: `src/Oficina.Api/Oficina.Api.csproj`
- Modify: `src/Oficina.Api/Program.cs`
- Create: `tests/Oficina.Integracao.Testes/MetricasPrometheusTestes.cs`

- [ ] **Step 1: Adicionar os pacotes OpenTelemetry ao `Oficina.Api.csproj`**

Adicionar ao `ItemGroup` de `PackageReference` de `src/Oficina.Api/Oficina.Api.csproj` (o exporter Prometheus AspNetCore é distribuído como pré-release `-beta`; versão explícita é aceita pelo NuGet):
```xml
    <PackageReference Include="OpenTelemetry.Extensions.Hosting" Version="1.9.0" />
    <PackageReference Include="OpenTelemetry.Instrumentation.AspNetCore" Version="1.9.0" />
    <PackageReference Include="OpenTelemetry.Instrumentation.Runtime" Version="1.9.0" />
    <PackageReference Include="OpenTelemetry.Exporter.Prometheus.AspNetCore" Version="1.9.0-beta.2" />
```

- [ ] **Step 2: Registrar OpenTelemetry (métricas) no `Program.cs`**

Em `src/Oficina.Api/Program.cs`, adicionar o `using` no topo (junto aos demais):
```csharp
using OpenTelemetry.Metrics;
```
E inserir o registro **logo após** a linha `builder.Services.AdicionarWebhook(builder.Configuration);` (linha ~60, antes do bloco do modo migrate):
```csharp

// Observabilidade minima: OpenTelemetry expondo /metrics (Prometheus).
// Instrumenta requests do ASP.NET Core + runtime .NET; Serilog segue para stdout.
builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddRuntimeInstrumentation()
        .AddPrometheusExporter());
```

- [ ] **Step 3: Publicar o endpoint `/metrics` no pipeline**

Em `src/Oficina.Api/Program.cs`, adicionar **logo após** `app.MapControllers();` (antes de `app.Run();`):
```csharp

// Endpoint de scraping do Prometheus (anonimo): expõe /metrics.
app.MapPrometheusScrapingEndpoint();
```

- [ ] **Step 4: Escrever o teste (sem Docker) de `/metrics`**

Create `tests/Oficina.Integracao.Testes/MetricasPrometheusTestes.cs`:
```csharp
using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Oficina.Infraestrutura.Auth;
using Xunit;

namespace Oficina.Integracao.Testes;

public class MetricasPrometheusTestes : IClassFixture<MetricasPrometheusTestes.MetricasFactory>
{
    private readonly MetricasFactory _factory;

    public MetricasPrometheusTestes(MetricasFactory factory) => _factory = factory;

    [Fact]
    public async Task GetMetrics_DeveRetornar200_NoFormatoPrometheus()
    {
        var client = _factory.CreateClient();

        // Gera ao menos uma métrica de request antes de raspar.
        await client.GetAsync("/health");

        var response = await client.GetAsync("/metrics");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        // O formato de exposição Prometheus sempre traz linhas de metadados "# HELP"/"# TYPE".
        body.Should().Contain("# HELP");
        body.Should().Contain("# TYPE");
    }

    /// <summary>
    /// Sobe o host sem Docker/Postgres: remove o BootstrapAdminHostedService
    /// (que tentaria migrar) e fornece JWT via env. /metrics não toca em banco.
    /// </summary>
    public class MetricasFactory : WebApplicationFactory<Program>
    {
        public MetricasFactory()
        {
            Environment.SetEnvironmentVariable("Jwt__Secret", new string('a', 64));
            Environment.SetEnvironmentVariable("Jwt__Issuer", "oficina-api-test");
            Environment.SetEnvironmentVariable("Jwt__Audience", "oficina-clients-test");
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                var hosted = services.SingleOrDefault(d =>
                    d.ServiceType == typeof(IHostedService) &&
                    d.ImplementationType == typeof(BootstrapAdminHostedService));
                if (hosted is not null) services.Remove(hosted);
            });
        }
    }
}
```

- [ ] **Step 5: Restaurar/compilar a solution**

Run: `dotnet build Oficina.sln`
Expected: **Build succeeded**, 0 erros (NuGet restaura o pacote `-beta.2` por versão explícita).

- [ ] **Step 6: Rodar o teste de `/metrics` (sem Docker, via filtro)**

Run: `dotnet test tests/Oficina.Integracao.Testes --filter "FullyQualifiedName~MetricasPrometheusTestes"`
Expected: **PASS** (1 teste). A `MetricasFactory` remove o hosted service, então boota sem Postgres. (NÃO rode a suíte de integração inteira — os demais testes usam Testcontainers/Docker, indisponível aqui.)

- [ ] **Step 7: Rodar as suítes unitárias (sanity de não-regressão)**

Run (uma por chamada):
```bash
dotnet test tests/Oficina.Dominio.Testes
dotnet test tests/Oficina.Aplicacao.Testes
dotnet test tests/Oficina.Adaptadores.Testes
dotnet test tests/Oficina.Infraestrutura.Testes
```
Expected: PASS em todas (nada de comportamento mudou; só adição de observabilidade).

- [ ] **Step 8: Commit**

```bash
cd "C:/Users/thiago/source/repos/Fiap/fiap_15SOAT_fase1"
git add src/Oficina.Api/Oficina.Api.csproj src/Oficina.Api/Program.cs tests/Oficina.Integracao.Testes/MetricasPrometheusTestes.cs
git commit -F - <<'EOF'
feat(obs): expõe /metrics via OpenTelemetry (Prometheus) mantendo Serilog

Instrumentacao de request (ASP.NET Core) + runtime .NET com Prometheus exporter
em /metrics (anonimo, para scraping). Serilog e o contrato HTTP inalterados.
Teste sem Docker (factory remove o hosted service) valida /metrics no formato
Prometheus.

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
EOF
```

---

## Nota de verificação

O endpoint `/metrics` fica anônimo por padrão (não há política de autorização global; o pipeline só exige `[Authorize]` onde declarado). Em produção, o acesso ao `/metrics` normalmente é restrito por rede/NetworkPolicy — fora do escopo do MVP. O HPA do Plano 08 usa o **metrics-server** (CPU/memória), não este `/metrics`; o `/metrics` é para **visibilidade** (Prometheus), encostando no módulo 8 sem subir a stack completa.
