# Fase 2 — Plano 08: Orquestração Kubernetes

> **For agentic workers:** REQUIRED SUB-SKILL: `superpowers:subagent-driven-development`. Steps usam checkbox (`- [ ]`). Task 1 é código testável (TDD onde der); Task 2 é manifesto YAML validado estaticamente. **Um commit por Task.** NÃO fazer merge/push.

**Goal:** Preparar a aplicação para rodar em Kubernetes (EKS). Duas frentes: (1) uma pequena mudança de **código** que torna a migração do banco "Job-able" — em K8s os pods do `Deployment` **não** migram (evita corrida entre réplicas); um `Job` dedicado migra + faz bootstrap do admin uma única vez antes do rollout. No compose/local/testes o comportamento atual (migrar no startup) é preservado por default. (2) Um conjunto de **manifestos YAML** em `k8s/` (Namespace, ConfigMap, Secret-template, Deployment, Service, HPA, Job de migração, README).

**Architecture:** Clean Architecture já existente (`Dominio ← Aplicacao ← Api → Infraestrutura`). A lógica de `MigrateAsync` + bootstrap do admin, hoje embutida em `BootstrapAdminHostedService.StartAsync` (Frameworks & Drivers), é extraída para um componente reutilizável `IInicializadorBanco` / `InicializadorBanco` em `Oficina.Infraestrutura/Persistencia/`. O `HostedService` passa a ser um fino orquestrador: só chama o inicializador **se** a flag `Bootstrap:ExecutarNoStartup` (default **true**) permitir. `Program.cs` ganha um "modo migrate" (via `args`/env) que resolve o inicializador, roda migração+bootstrap e **sai sem subir o servidor web** — é o entrypoint do `Job` de migração no K8s. No K8s o `ConfigMap` seta `Bootstrap__ExecutarNoStartup=false` (pods não migram) e o `Job` roda no modo migrate (que migra+bootstrap independentemente da flag, pois o HostedService nem chega a rodar nesse modo).

**Tech Stack:** C# 12 / .NET 8, ASP.NET Core, EF Core 8 (Npgsql). Kubernetes `apps/v1` (Deployment), `batch/v1` (Job), `autoscaling/v2` (HPA), `v1` (Namespace/Service/ConfigMap/Secret). Imagem: a mesma do `docker/Dockerfile` (porta 8080, usuário `app` non-root, `ENTRYPOINT ["dotnet","Oficina.Api.dll"]`). RDS PostgreSQL 16 e `metrics-server` vêm do Terraform (Plano 09).

## Global Constraints

- **Idioma pt-BR** em código, identificadores, comentários e mensagens de commit.
- **Target:** `net8.0`. **`public partial class Program { }` DEVE ser mantida** (usada por `WebApplicationFactory<Program>` nos testes de integração).
- **Docker/kubectl/helm NÃO estão instalados localmente.** Manifestos são validados **estaticamente** (apiVersions, selectors, nomes de env) e no **CI/deploy**. O único gate LOCAL é a Task de CÓDIGO: `dotnet build Oficina.sln` (0 erros) + testes unitários verdes (**um `dotnet test` por projeto** — em multi-projeto o CLI dispara `MSB1008`).
- **Não quebrar** o contrato HTTP nem o `docker-compose`. Compose/local/testes continuam migrando no startup por default (sem a chave `Bootstrap:ExecutarNoStartup` → `true`).
- **NUNCA commitar segredo real.** `k8s/secret.yaml` é um TEMPLATE com placeholders óbvios (`TROCAR_NO_DEPLOY`); o secret real é criado pelo CI a partir de GitHub Secrets + outputs do Terraform.
- **Branch:** `fase-2` (sem merge/push). **Bash tool = Git Bash.** Um commit por Task, mensagem pt-BR terminando com:
  `Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>`

## Estrutura de arquivos

```
# Task 1 (código)
src/Oficina.Infraestrutura/Persistencia/InicializadorBanco.cs        NOVO  (IInicializadorBanco + InicializadorBanco)
src/Oficina.Infraestrutura/Auth/BootstrapAdminHostedService.cs       MOD   (delega ao inicializador; respeita a flag)
src/Oficina.Infraestrutura/DependencyInjection.cs                    MOD   (registra IInicializadorBanco)
src/Oficina.Api/Program.cs                                           MOD   (modo "migrate": roda e sai)
src/Oficina.Api/appsettings.json                                     MOD   (documenta Bootstrap:ExecutarNoStartup = true)
tests/Oficina.Infraestrutura.Testes/Oficina.Infraestrutura.Testes.csproj   NOVO  (projeto de teste unitário, sem Docker)
tests/Oficina.Infraestrutura.Testes/Persistencia/InicializadorBancoTestes.cs         NOVO
tests/Oficina.Infraestrutura.Testes/Auth/BootstrapAdminHostedServiceTestes.cs        NOVO
Oficina.sln                                                          MOD   (dotnet sln add do novo projeto)

# Task 2 (manifestos — validação estática/CI)
k8s/namespace.yaml         NOVO
k8s/configmap.yaml         NOVO
k8s/secret.yaml            NOVO  (TEMPLATE, placeholders)
k8s/deployment.yaml        NOVO
k8s/service.yaml           NOVO
k8s/hpa.yaml               NOVO
k8s/migration-job.yaml     NOVO
k8s/README.md              NOVO
```

---

## Task 1: Tornar a migração "Job-able" (CÓDIGO — testável)

**Files:**
- Create: `src/Oficina.Infraestrutura/Persistencia/InicializadorBanco.cs`
- Modify: `src/Oficina.Infraestrutura/Auth/BootstrapAdminHostedService.cs`
- Modify: `src/Oficina.Infraestrutura/DependencyInjection.cs`
- Modify: `src/Oficina.Api/Program.cs`
- Modify: `src/Oficina.Api/appsettings.json`
- Create: `tests/Oficina.Infraestrutura.Testes/Oficina.Infraestrutura.Testes.csproj`
- Create: `tests/Oficina.Infraestrutura.Testes/Persistencia/InicializadorBancoTestes.cs`
- Create: `tests/Oficina.Infraestrutura.Testes/Auth/BootstrapAdminHostedServiceTestes.cs`
- Modify: `Oficina.sln`

> **Estado atual (para referência):** `BootstrapAdminHostedService.StartAsync` cria um escopo, resolve `OficinaDbContext`, roda `db.Database.MigrateAsync`, lê `AdminBootstrap:Password` e (se presente) resolve/roda `BootstrapAdminUseCase`. Registrado em `DependencyInjectionAuth.AdicionarAutenticacao` via `services.AddHostedService<BootstrapAdminHostedService>()`. `Program.cs` termina em `app.Run();` seguido de `public partial class Program { }`.
>
> **Decisão de design:** `InicializadorBanco` implementa a interface `IInicializadorBanco` (registrada em DI). Isso permite **injetar um duplo** no `HostedService` e testar, sem Docker/DB, que a flag `ExecutarNoStartup=false` faz o pod **não** chamar o inicializador (comportamento crítico em K8s). O método público estático `DeveExecutarNoStartup(IConfiguration)` é puro e trivialmente testável.

- [ ] **Step 1 (TDD — teste primeiro do parsing da flag): criar o projeto de teste unitário e o teste de `DeveExecutarNoStartup`.**

  Criar `tests/Oficina.Infraestrutura.Testes/Oficina.Infraestrutura.Testes.csproj` (projeto de teste unitário, **sem Testcontainers/Docker**):

  ```xml
  <Project Sdk="Microsoft.NET.Sdk">

    <PropertyGroup>
      <TargetFramework>net8.0</TargetFramework>
      <ImplicitUsings>enable</ImplicitUsings>
      <Nullable>enable</Nullable>

      <IsPackable>false</IsPackable>
      <IsTestProject>true</IsTestProject>
    </PropertyGroup>

    <ItemGroup>
      <PackageReference Include="coverlet.collector" Version="6.0.2">
        <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
        <PrivateAssets>all</PrivateAssets>
      </PackageReference>
      <PackageReference Include="FluentAssertions" Version="6.12.1" />
      <PackageReference Include="Microsoft.Extensions.Configuration" Version="8.0.0" />
      <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="8.0.2" />
      <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.8.0" />
      <PackageReference Include="Moq" Version="4.20.72" />
      <PackageReference Include="xunit" Version="2.5.3" />
      <PackageReference Include="xunit.runner.visualstudio" Version="2.5.3" />
    </ItemGroup>

    <ItemGroup>
      <Using Include="Xunit" />
    </ItemGroup>

    <ItemGroup>
      <ProjectReference Include="..\..\src\Oficina.Infraestrutura\Oficina.Infraestrutura.csproj" />
    </ItemGroup>

  </Project>
  ```

  Criar `tests/Oficina.Infraestrutura.Testes/Persistencia/InicializadorBancoTestes.cs`:

  ```csharp
  using FluentAssertions;
  using Microsoft.Extensions.Configuration;
  using Oficina.Infraestrutura.Persistencia;

  namespace Oficina.Infraestrutura.Testes.Persistencia;

  public class InicializadorBancoTestes
  {
      private static IConfiguration Config(string? valor)
      {
          var dados = new Dictionary<string, string?>();
          if (valor is not null)
              dados["Bootstrap:ExecutarNoStartup"] = valor;

          return new ConfigurationBuilder()
              .AddInMemoryCollection(dados)
              .Build();
      }

      [Fact]
      public void SemAChave_Default_DeveSerTrue()
      {
          InicializadorBanco.DeveExecutarNoStartup(Config(null)).Should().BeTrue();
      }

      [Theory]
      [InlineData("true", true)]
      [InlineData("True", true)]
      [InlineData("false", false)]
      [InlineData("False", false)]
      public void ComAChave_DeveRespeitarOValor(string valor, bool esperado)
      {
          InicializadorBanco.DeveExecutarNoStartup(Config(valor)).Should().Be(esperado);
      }

      [Fact]
      public void ValorInvalido_DeveCairNoDefaultTrue()
      {
          InicializadorBanco.DeveExecutarNoStartup(Config("nao-booleano")).Should().BeTrue();
      }
  }
  ```

  Adicionar o projeto à solution (necessário para o CI enumerar via `dotnet test` na solution):

  ```bash
  dotnet sln "C:/Users/thiago/source/repos/Fiap/fiap_15SOAT_fase1/Oficina.sln" \
    add "C:/Users/thiago/source/repos/Fiap/fiap_15SOAT_fase1/tests/Oficina.Infraestrutura.Testes/Oficina.Infraestrutura.Testes.csproj"
  ```

  Neste ponto o teste **não compila** (ainda não existe `InicializadorBanco`) — esperado no TDD. Segue o Step 2 para criar o SUT.

- [ ] **Step 2: criar `src/Oficina.Infraestrutura/Persistencia/InicializadorBanco.cs`** (interface + implementação; lógica movida do HostedService).

  ```csharp
  using Microsoft.EntityFrameworkCore;
  using Microsoft.Extensions.Configuration;
  using Microsoft.Extensions.DependencyInjection;
  using Microsoft.Extensions.Logging;
  using Oficina.Aplicacao.Auth;

  namespace Oficina.Infraestrutura.Persistencia;

  /// <summary>
  /// Aplica as migrations pendentes e, se houver senha de bootstrap configurada,
  /// cria o usuário admin inicial. Reutilizado tanto pelo startup do host
  /// (compose/local) quanto pelo Job de migração do Kubernetes.
  /// </summary>
  public interface IInicializadorBanco
  {
      Task ExecutarAsync(IServiceProvider sp, IConfiguration config, ILogger log, CancellationToken ct);
  }

  public sealed class InicializadorBanco : IInicializadorBanco
  {
      /// <summary>
      /// Indica se a inicialização (migração + bootstrap) deve ocorrer no startup do host.
      /// Sem a chave (compose/local/testes) → true. Em Kubernetes o ConfigMap define
      /// "Bootstrap:ExecutarNoStartup" = "false" para que os pods do Deployment NÃO migrem
      /// (a migração é feita pelo Job dedicado). Valor inválido cai no default true.
      /// </summary>
      public static bool DeveExecutarNoStartup(IConfiguration config)
      {
          var valor = config["Bootstrap:ExecutarNoStartup"];
          return string.IsNullOrWhiteSpace(valor)
              || !bool.TryParse(valor, out var habilitado)
              || habilitado;
      }

      public async Task ExecutarAsync(
          IServiceProvider sp,
          IConfiguration config,
          ILogger log,
          CancellationToken ct)
      {
          using var scope = sp.CreateScope();
          var services = scope.ServiceProvider;

          var db = services.GetRequiredService<OficinaDbContext>();
          log.LogInformation("Aplicando migrations...");
          await db.Database.MigrateAsync(ct);

          var senha = config["AdminBootstrap:Password"];
          if (string.IsNullOrWhiteSpace(senha))
          {
              log.LogWarning("AdminBootstrap:Password não configurado — bootstrap pulado.");
              return;
          }

          var bootstrap = services.GetRequiredService<BootstrapAdminUseCase>();
          await bootstrap.ExecutarAsync(senha, ct);
      }
  }
  ```

  Rodar o teste do Step 1 (parsing) — deve ficar **verde**:

  ```bash
  dotnet test "C:/Users/thiago/source/repos/Fiap/fiap_15SOAT_fase1/tests/Oficina.Infraestrutura.Testes/Oficina.Infraestrutura.Testes.csproj"
  ```

- [ ] **Step 3 (TDD — teste do HostedService respeitando a flag): criar `tests/Oficina.Infraestrutura.Testes/Auth/BootstrapAdminHostedServiceTestes.cs`.**

  ```csharp
  using Microsoft.Extensions.Configuration;
  using Microsoft.Extensions.Logging.Abstractions;
  using Moq;
  using Oficina.Infraestrutura.Auth;
  using Oficina.Infraestrutura.Persistencia;

  namespace Oficina.Infraestrutura.Testes.Auth;

  public class BootstrapAdminHostedServiceTestes
  {
      private static IConfiguration Config(string? executarNoStartup)
      {
          var dados = new Dictionary<string, string?>();
          if (executarNoStartup is not null)
              dados["Bootstrap:ExecutarNoStartup"] = executarNoStartup;

          return new ConfigurationBuilder().AddInMemoryCollection(dados).Build();
      }

      private static BootstrapAdminHostedService Criar(
          IConfiguration config,
          Mock<IInicializadorBanco> inicializador)
      {
          // O IServiceProvider não é tocado no caminho "flag=false" e é ignorado
          // pelo duplo no caminho "flag=true", então um provider mínimo basta.
          var sp = Mock.Of<IServiceProvider>();
          return new BootstrapAdminHostedService(
              sp,
              config,
              NullLogger<BootstrapAdminHostedService>.Instance,
              inicializador.Object);
      }

      [Fact]
      public async Task ExecutarNoStartupFalse_NaoChamaOInicializador()
      {
          var inicializador = new Mock<IInicializadorBanco>();
          var svc = Criar(Config("false"), inicializador);

          await svc.StartAsync(CancellationToken.None);

          inicializador.Verify(
              i => i.ExecutarAsync(It.IsAny<IServiceProvider>(), It.IsAny<IConfiguration>(),
                  It.IsAny<Microsoft.Extensions.Logging.ILogger>(), It.IsAny<CancellationToken>()),
              Times.Never);
      }

      [Fact]
      public async Task SemAChave_Default_ChamaOInicializador()
      {
          var inicializador = new Mock<IInicializadorBanco>();
          var svc = Criar(Config(null), inicializador);

          await svc.StartAsync(CancellationToken.None);

          inicializador.Verify(
              i => i.ExecutarAsync(It.IsAny<IServiceProvider>(), It.IsAny<IConfiguration>(),
                  It.IsAny<Microsoft.Extensions.Logging.ILogger>(), It.IsAny<CancellationToken>()),
              Times.Once);
      }

      [Fact]
      public async Task ExecutarNoStartupTrue_ChamaOInicializador()
      {
          var inicializador = new Mock<IInicializadorBanco>();
          var svc = Criar(Config("true"), inicializador);

          await svc.StartAsync(CancellationToken.None);

          inicializador.Verify(
              i => i.ExecutarAsync(It.IsAny<IServiceProvider>(), It.IsAny<IConfiguration>(),
                  It.IsAny<Microsoft.Extensions.Logging.ILogger>(), It.IsAny<CancellationToken>()),
              Times.Once);
      }
  }
  ```

  Não compila ainda (o construtor do HostedService não recebe `IInicializadorBanco`) — esperado. Segue o Step 4.

- [ ] **Step 4: refatorar `src/Oficina.Infraestrutura/Auth/BootstrapAdminHostedService.cs`** para delegar ao inicializador e respeitar a flag. **Substituir o arquivo inteiro** por:

  ```csharp
  using Microsoft.Extensions.Configuration;
  using Microsoft.Extensions.Hosting;
  using Microsoft.Extensions.Logging;
  using Oficina.Infraestrutura.Persistencia;

  namespace Oficina.Infraestrutura.Auth;

  /// <summary>
  /// No startup do host (compose/local/testes) aplica migrations + bootstrap do admin.
  /// Em Kubernetes o ConfigMap define Bootstrap:ExecutarNoStartup=false, então os pods
  /// do Deployment NÃO migram — quem migra é o Job dedicado (modo "migrate" do Program).
  /// </summary>
  public class BootstrapAdminHostedService : IHostedService
  {
      private readonly IServiceProvider _sp;
      private readonly IConfiguration _config;
      private readonly ILogger<BootstrapAdminHostedService> _log;
      private readonly IInicializadorBanco _inicializador;

      public BootstrapAdminHostedService(
          IServiceProvider sp,
          IConfiguration config,
          ILogger<BootstrapAdminHostedService> log,
          IInicializadorBanco inicializador)
      {
          _sp = sp;
          _config = config;
          _log = log;
          _inicializador = inicializador;
      }

      public async Task StartAsync(CancellationToken cancellationToken)
      {
          if (!InicializadorBanco.DeveExecutarNoStartup(_config))
          {
              _log.LogInformation(
                  "Bootstrap:ExecutarNoStartup=false — inicialização no startup pulada " +
                  "(modo Kubernetes; a migração é feita pelo Job dedicado).");
              return;
          }

          await _inicializador.ExecutarAsync(_sp, _config, _log, cancellationToken);
      }

      public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
  }
  ```

- [ ] **Step 5: registrar o inicializador em `src/Oficina.Infraestrutura/DependencyInjection.cs`.**

  Adicionar dentro de `AdicionarInfraestrutura`, logo após o `AddDbContext<OficinaDbContext>(...)` (o `using Oficina.Infraestrutura.Persistencia;` já existe no arquivo):

  ```csharp
  // Reutilizável pelo startup (HostedService) e pelo Job de migração (Program "migrate").
  // Singleton stateless: cria o próprio escopo a partir do IServiceProvider recebido.
  services.AddSingleton<IInicializadorBanco, InicializadorBanco>();
  ```

  > Nota de captive dependency: `BootstrapAdminHostedService` é singleton (hosted service). Ele injeta `IInicializadorBanco` (**singleton**, ok), `IConfiguration`/`ILogger<>` (singletons) e `IServiceProvider` (raiz). Não há dependência scoped capturada. O escopo é criado dentro de `ExecutarAsync`.

  Rodar o teste do Step 3 — deve ficar **verde**:

  ```bash
  dotnet test "C:/Users/thiago/source/repos/Fiap/fiap_15SOAT_fase1/tests/Oficina.Infraestrutura.Testes/Oficina.Infraestrutura.Testes.csproj"
  ```

- [ ] **Step 6: adicionar o "modo migrate" em `src/Oficina.Api/Program.cs`.**

  Adicionar `using Oficina.Infraestrutura.Persistencia;` no topo (junto aos demais `using`) e inserir o bloco de migrate **logo antes de `var app = builder.Build();`** (linha 61 atual), após todas as chamadas `Adicionar*`. O `Program.cs` completo passa a ser:

  ```csharp
  using FluentValidation.AspNetCore;
  using Oficina.Adaptadores;
  using Oficina.Aplicacao;
  using Oficina.Api.Configuracao;
  using Oficina.Infraestrutura;
  using Oficina.Infraestrutura.Persistencia;
  using Serilog;

  var builder = WebApplication.CreateBuilder(args);

  builder.Host.UseSerilog((context, services, configuration) =>
  {
      configuration.ReadFrom.Configuration(context.Configuration);
  });

  builder.Services.AddControllers();
  builder.Services
      .AddFluentValidationAutoValidation()
      .AddFluentValidationClientsideAdapters();
  builder.Services.AddEndpointsApiExplorer();
  builder.Services.AddSwaggerGen(options =>
  {
      options.SwaggerDoc("v1", new()
      {
          Title = "Oficina Mecânica API",
          Version = "v1",
          Description = "API do MVP do Sistema Integrado de Atendimento e Execução de Serviços."
      });
      options.AddSecurityDefinition("Bearer", new()
      {
          Name = "Authorization",
          Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
          Scheme = "Bearer",
          BearerFormat = "JWT",
          In = Microsoft.OpenApi.Models.ParameterLocation.Header,
          Description = "Bearer JWT"
      });
      options.AddSecurityRequirement(new()
      {
          {
              new()
              {
                  Reference = new()
                  {
                      Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                      Id = "Bearer"
                  }
              },
              Array.Empty<string>()
          }
      });
  });

  builder.Services.AdicionarInfraestrutura(builder.Configuration);
  builder.Services.AdicionarAplicacao();
  builder.Services.AdicionarAdaptadores();
  builder.Services.AdicionarJwtBearer(builder.Configuration);
  builder.Services.AdicionarPoliticas();
  builder.Services.AdicionarRateLimit(builder.Configuration);
  builder.Services.AdicionarWebhook(builder.Configuration);

  // Modo Job de migração (Kubernetes): "dotnet Oficina.Api.dll migrate" ou STARTUP_TASK=migrate.
  // Aplica migrations + bootstrap do admin e ENCERRA sem subir o servidor web.
  // Roda o inicializador diretamente (o HostedService não é iniciado neste caminho,
  // pois builder.Build() não dispara hosted services — só app.Run() faria).
  var tarefaStartup = Environment.GetEnvironmentVariable("STARTUP_TASK");
  if (args.Contains("migrate") ||
      string.Equals(tarefaStartup, "migrate", StringComparison.OrdinalIgnoreCase))
  {
      await using var appMigrate = builder.Build();
      var logMigrate = appMigrate.Services
          .GetRequiredService<ILoggerFactory>()
          .CreateLogger("Migrate");
      var inicializador = appMigrate.Services.GetRequiredService<IInicializadorBanco>();

      logMigrate.LogInformation("STARTUP_TASK=migrate — migração + bootstrap; encerrando após concluir.");
      await inicializador.ExecutarAsync(
          appMigrate.Services, appMigrate.Configuration, logMigrate, CancellationToken.None);
      return;
  }

  var app = builder.Build();

  if (app.Environment.IsDevelopment())
  {
      app.UseSwagger();
      app.UseSwaggerUI(options =>
      {
          options.SwaggerEndpoint("/swagger/v1/swagger.json", "Oficina Mecânica API v1");
      });
  }

  app.UseSerilogRequestLogging();
  app.UseMiddleware<MiddlewareDeExcecoes>();
  app.UseAuthentication();
  app.UseAuthorization();
  app.UseRateLimiter();
  app.MapControllers();

  app.Run();

  public partial class Program { }
  ```

  > `GetRequiredService`, `ILoggerFactory` e `IConfiguration` já estão nos implicit usings do SDK Web — só `Oficina.Infraestrutura.Persistencia` precisa ser importado. Introduzir `await` no topo transforma o Main gerado em assíncrono (suportado por top-level statements). `WebApplicationFactory<Program>` continua subindo o app normalmente (sem arg `migrate` e sem `STARTUP_TASK`), preservando os testes de integração.

- [ ] **Step 7: documentar a flag em `src/Oficina.Api/appsettings.json`** (default `true`). Substituir o arquivo por:

  ```json
  {
    "Serilog": {
      "MinimumLevel": {
        "Default": "Information",
        "Override": {
          "Microsoft": "Warning",
          "Microsoft.Hosting.Lifetime": "Information",
          "Microsoft.EntityFrameworkCore": "Warning"
        }
      },
      "WriteTo": [
        {
          "Name": "Console",
          "Args": {
            "formatter": "Serilog.Formatting.Compact.RenderedCompactJsonFormatter, Serilog.Formatting.Compact"
          }
        }
      ],
      "Enrich": ["FromLogContext", "WithMachineName", "WithProcessId"]
    },
    "ConnectionStrings": {
      "Default": "Host=localhost;Port=5432;Database=oficina;Username=oficina;Password=oficina"
    },
    "Bootstrap": {
      "ExecutarNoStartup": true
    },
    "Webhook": {
      "Token": ""
    },
    "AllowedHosts": "*"
  }
  ```

  > `Bootstrap:ExecutarNoStartup = true` no arquivo torna o default explícito/documentado. Em K8s o ConfigMap sobrescreve com `Bootstrap__ExecutarNoStartup="false"`.

- [ ] **Step 8: GATE de Task 1 — build + testes unitários (um `dotnet test` por projeto; evita `MSB1008`).**

  ```bash
  cd "C:/Users/thiago/source/repos/Fiap/fiap_15SOAT_fase1"
  dotnet build Oficina.sln
  dotnet test tests/Oficina.Dominio.Testes/Oficina.Dominio.Testes.csproj
  dotnet test tests/Oficina.Aplicacao.Testes/Oficina.Aplicacao.Testes.csproj
  dotnet test tests/Oficina.Adaptadores.Testes/Oficina.Adaptadores.Testes.csproj
  dotnet test tests/Oficina.Infraestrutura.Testes/Oficina.Infraestrutura.Testes.csproj
  ```

  Esperado: `dotnet build` **0 erros**; os 4 projetos de teste **verdes**. `Oficina.Integracao.Testes` (Testcontainers) **NÃO** roda local (sem Docker) — roda no CI; como não seta `Bootstrap__ExecutarNoStartup`, mantém o default `true` e `BootstrapAdminTestes` continua válido.

- [ ] **Step 9: commit da Task 1** (mensagem pt-BR).

  ```bash
  cd "C:/Users/thiago/source/repos/Fiap/fiap_15SOAT_fase1"
  git add -A
  git commit -m "$(cat <<'EOF'
  feat(infra): migração "Job-able" via InicializadorBanco e flag de startup

  Extrai MigrateAsync + bootstrap do admin para IInicializadorBanco/InicializadorBanco,
  reutilizado pelo startup do host e pelo Job de migração do Kubernetes. O
  BootstrapAdminHostedService passa a respeitar Bootstrap:ExecutarNoStartup (default true,
  preservando compose/local/testes) e Program ganha o modo "migrate" (args/STARTUP_TASK)
  que migra+bootstrap e encerra sem subir o servidor web. Novo projeto de testes unitarios
  Oficina.Infraestrutura.Testes cobre o parsing da flag e o respeito a ela pelo HostedService.

  Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
  EOF
  )"
  ```

---

## Task 2: Manifestos Kubernetes (`k8s/` — validação estática / CI)

**Files:** criar `k8s/namespace.yaml`, `k8s/configmap.yaml`, `k8s/secret.yaml`, `k8s/deployment.yaml`, `k8s/service.yaml`, `k8s/hpa.yaml`, `k8s/migration-job.yaml`, `k8s/README.md`.

> **Sem kubectl local.** Cada manifesto é validado por inspeção + `grep` (Step 9). Aplicação real acontece no CI/deploy do Plano 09. `${ECR_REPOSITORY}` e `${IMAGE_TAG}` são placeholders substituídos pelo CI (via `envsubst`/`sed`) antes do `kubectl apply`.

- [ ] **Step 1: `k8s/namespace.yaml`**

  ```yaml
  apiVersion: v1
  kind: Namespace
  metadata:
    name: oficina
    labels:
      app.kubernetes.io/part-of: oficina
  ```

- [ ] **Step 2: `k8s/configmap.yaml`** — valores NÃO-sensíveis.

  ```yaml
  apiVersion: v1
  kind: ConfigMap
  metadata:
    name: oficina-api-config
    namespace: oficina
    labels:
      app: oficina-api
  data:
    ASPNETCORE_ENVIRONMENT: "Production"
    # Emissor/audiência do JWT (não sensíveis). Devem bater com o token emitido pela API.
    Jwt__Issuer: "oficina-api"
    Jwt__Audience: "oficina-clients"
    # Em Kubernetes os pods do Deployment NÃO migram: a migração é feita pelo Job dedicado.
    Bootstrap__ExecutarNoStartup: "false"
    # OBS: a connection string completa contém a SENHA do banco → fica no Secret
    # (chave ConnectionStrings__Default), não aqui. O endpoint do RDS PostgreSQL vem
    # do output do Terraform (Plano 09) e é injetado na criação do Secret pelo CI.
  ```

- [ ] **Step 3: `k8s/secret.yaml`** — TEMPLATE com placeholders (NÃO commitar valores reais).

  ```yaml
  # TEMPLATE — NÃO CONTÉM SEGREDOS REAIS.
  # No deploy real este Secret é criado pelo CI a partir de GitHub Secrets + outputs do
  # Terraform (endpoint/credenciais do RDS), por exemplo:
  #   kubectl create secret generic oficina-api-secret -n oficina \
  #     --from-literal=Jwt__Secret="$JWT_SECRET" \
  #     --from-literal=ConnectionStrings__Default="Host=$RDS_HOST;Port=5432;Database=oficina;Username=$DB_USER;Password=$DB_PASSWORD" \
  #     --from-literal=AdminBootstrap__Password="$ADMIN_BOOTSTRAP_PASSWORD" \
  #     --from-literal=Webhook__Token="$WEBHOOK_TOKEN"
  # Este arquivo serve apenas como contrato/documentação das chaves. NUNCA commitar valores reais.
  apiVersion: v1
  kind: Secret
  metadata:
    name: oficina-api-secret
    namespace: oficina
    labels:
      app: oficina-api
  type: Opaque
  stringData:
    Jwt__Secret: "TROCAR_NO_DEPLOY_min_64_caracteres_aleatorios_para_HS256_xxxxxxxxxxxx"
    ConnectionStrings__Default: "Host=TROCAR_ENDPOINT_RDS;Port=5432;Database=oficina;Username=TROCAR_NO_DEPLOY;Password=TROCAR_NO_DEPLOY"
    AdminBootstrap__Password: "TROCAR_NO_DEPLOY"
    Webhook__Token: "TROCAR_NO_DEPLOY"
  ```

- [ ] **Step 4: `k8s/deployment.yaml`**

  ```yaml
  apiVersion: apps/v1
  kind: Deployment
  metadata:
    name: oficina-api
    namespace: oficina
    labels:
      app: oficina-api
  spec:
    replicas: 2
    selector:
      matchLabels:
        app: oficina-api
    template:
      metadata:
        labels:
          app: oficina-api
      spec:
        securityContext:
          runAsNonRoot: true
          seccompProfile:
            type: RuntimeDefault
        containers:
          - name: oficina-api
            # ${ECR_REPOSITORY} e ${IMAGE_TAG} são substituídos pelo CI antes do apply
            # (ex.: envsubst < k8s/deployment.yaml | kubectl apply -f -).
            image: "${ECR_REPOSITORY}:${IMAGE_TAG}"
            ports:
              - name: http
                containerPort: 8080
            envFrom:
              - configMapRef:
                  name: oficina-api-config
              - secretRef:
                  name: oficina-api-secret
            resources:
              requests:
                cpu: "100m"
                memory: "128Mi"
              limits:
                cpu: "500m"
                memory: "256Mi"
            readinessProbe:
              httpGet:
                path: /health
                port: 8080
              initialDelaySeconds: 5
              periodSeconds: 10
              timeoutSeconds: 3
              failureThreshold: 3
            livenessProbe:
              httpGet:
                path: /health
                port: 8080
              initialDelaySeconds: 15
              periodSeconds: 20
              timeoutSeconds: 3
              failureThreshold: 3
            securityContext:
              runAsNonRoot: true
              allowPrivilegeEscalation: false
              readOnlyRootFilesystem: true
              capabilities:
                drop:
                  - ALL
            volumeMounts:
              # rootfs read-only → /tmp precisa ser gravável (arquivos temporários do .NET).
              - name: tmp
                mountPath: /tmp
        volumes:
          - name: tmp
            emptyDir: {}
  ```

  > A imagem `docker/Dockerfile` já roda como `USER app` (non-root) e expõe 8080 (`ASPNETCORE_URLS=http://+:8080`), então `runAsNonRoot: true` e `containerPort: 8080` são coerentes. A app usa JWT HS256 (sem DataProtection persistente) → `readOnlyRootFilesystem: true` + `emptyDir` em `/tmp` é suficiente.

- [ ] **Step 5: `k8s/service.yaml`**

  ```yaml
  apiVersion: v1
  kind: Service
  metadata:
    name: oficina-api
    namespace: oficina
    labels:
      app: oficina-api
  spec:
    type: LoadBalancer
    selector:
      app: oficina-api
    ports:
      - name: http
        port: 80
        targetPort: 8080
        protocol: TCP
  ```

- [ ] **Step 6: `k8s/hpa.yaml`**

  ```yaml
  apiVersion: autoscaling/v2
  kind: HorizontalPodAutoscaler
  metadata:
    name: oficina-api
    namespace: oficina
    labels:
      app: oficina-api
  spec:
    scaleTargetRef:
      apiVersion: apps/v1
      kind: Deployment
      name: oficina-api
    minReplicas: 2
    maxReplicas: 10
    metrics:
      - type: Resource
        resource:
          name: cpu
          target:
            type: Utilization
            averageUtilization: 60
      - type: Resource
        resource:
          name: memory
          target:
            type: Utilization
            averageUtilization: 70
  ```

  > **Requer `metrics-server`** no cluster (instalado via Terraform/Helm no Plano 09). Sem ele o HPA fica com métricas `<unknown>` e não escala. As métricas de Utilization dependem dos `resources.requests` do Deployment (Step 4).

- [ ] **Step 7: `k8s/migration-job.yaml`**

  ```yaml
  apiVersion: batch/v1
  kind: Job
  metadata:
    name: oficina-migrate
    namespace: oficina
    labels:
      app: oficina-api
      component: migrate
  spec:
    backoffLimit: 3
    template:
      metadata:
        labels:
          app: oficina-api
          component: migrate
      spec:
        restartPolicy: Never
        securityContext:
          runAsNonRoot: true
          seccompProfile:
            type: RuntimeDefault
        containers:
          - name: migrate
            image: "${ECR_REPOSITORY}:${IMAGE_TAG}"
            # Modo migrate do Program.cs: migra + bootstrap e encerra (não sobe o servidor web).
            # Sobrescreve o ENTRYPOINT ["dotnet","Oficina.Api.dll"] do Dockerfile.
            command: ["dotnet", "Oficina.Api.dll", "migrate"]
            envFrom:
              - configMapRef:
                  name: oficina-api-config
              - secretRef:
                  name: oficina-api-secret
            resources:
              requests:
                cpu: "100m"
                memory: "128Mi"
              limits:
                cpu: "500m"
                memory: "256Mi"
            securityContext:
              runAsNonRoot: true
              allowPrivilegeEscalation: false
              readOnlyRootFilesystem: true
              capabilities:
                drop:
                  - ALL
            volumeMounts:
              - name: tmp
                mountPath: /tmp
        volumes:
          - name: tmp
            emptyDir: {}
  ```

  > O Job herda `Bootstrap__ExecutarNoStartup=false` do ConfigMap, mas isso é irrelevante no modo migrate: o `Program.cs` chama `InicializadorBanco.ExecutarAsync` diretamente (o HostedService não é iniciado quando o app não sobe). O `ConnectionStrings__Default` + `AdminBootstrap__Password` (do Secret) garantem migração **e** bootstrap. O CI aplica este Job e **aguarda concluir ANTES** do rollout do Deployment (ver README).

- [ ] **Step 8: `k8s/README.md`**

  ```markdown
  # Manifestos Kubernetes — Oficina API

  Orquestração da API no EKS (Fase 2). O endpoint do RDS PostgreSQL, o `metrics-server`
  e os segredos reais são provisionados pelo Terraform / CI (Plano 09).

  ## Arquivos

  | Arquivo | Recurso (apiVersion) | O que cria |
  |---|---|---|
  | `namespace.yaml` | Namespace (`v1`) | Namespace `oficina`. |
  | `configmap.yaml` | ConfigMap (`v1`) | `oficina-api-config`: config não-sensível (`ASPNETCORE_ENVIRONMENT`, `Jwt__Issuer`, `Jwt__Audience`, `Bootstrap__ExecutarNoStartup=false`). |
  | `secret.yaml` | Secret (`v1`) | `oficina-api-secret`: **TEMPLATE** com placeholders (`Jwt__Secret`, `ConnectionStrings__Default`, `AdminBootstrap__Password`, `Webhook__Token`). No deploy real é criado pelo CI a partir de GitHub Secrets + outputs do Terraform. |
  | `migration-job.yaml` | Job (`batch/v1`) | `oficina-migrate`: roda `dotnet Oficina.Api.dll migrate` (migração + bootstrap) e encerra. |
  | `deployment.yaml` | Deployment (`apps/v1`) | `oficina-api`: 2 réplicas, probes em `/health:8080`, resources requests/limits, securityContext restritivo, rootfs read-only + `emptyDir` em `/tmp`. |
  | `service.yaml` | Service (`v1`) | `oficina-api`: `LoadBalancer` `80 → 8080`. |
  | `hpa.yaml` | HorizontalPodAutoscaler (`autoscaling/v2`) | `oficina-api`: 2–10 réplicas, CPU ~60% / memória ~70%. |

  ## Ordem de aplicação

  As imagens usam os placeholders `${ECR_REPOSITORY}` e `${IMAGE_TAG}`, substituídos pelo CI
  (ex.: `envsubst < arquivo.yaml | kubectl apply -f -`).

  ```bash
  kubectl apply -f namespace.yaml
  kubectl apply -f configmap.yaml
  kubectl apply -f secret.yaml          # no deploy real: kubectl create secret (ver secret.yaml)
  kubectl apply -f migration-job.yaml   # migra + bootstrap
  kubectl wait --for=condition=complete job/oficina-migrate -n oficina --timeout=300s
  kubectl apply -f deployment.yaml      # rollout só depois do Job concluir
  kubectl apply -f service.yaml
  kubectl apply -f hpa.yaml             # requer metrics-server (Terraform, Plano 09)
  ```

  ## Dependências (Terraform — Plano 09)

  - **RDS PostgreSQL 16**: o endpoint alimenta `ConnectionStrings__Default` no Secret.
  - **metrics-server**: obrigatório para o HPA (sem ele as métricas ficam `<unknown>`).
  - **ECR**: repositório da imagem (`${ECR_REPOSITORY}`); a tag (`${IMAGE_TAG}`) é o SHA/versão do build do CI.

  ## Migração vs. startup

  Em Kubernetes os pods do Deployment **não** migram (`Bootstrap__ExecutarNoStartup=false`),
  evitando corrida entre réplicas. O `Job` `oficina-migrate` migra + faz bootstrap uma única
  vez, antes do rollout. No `docker-compose`/local o comportamento é o oposto por default
  (sem a chave → migra no startup), por conveniência.

  ## Alternativa (opcional): Ingress/ALB

  Em vez de `Service type: LoadBalancer`, pode-se usar `type: ClusterIP` + um `Ingress`
  (AWS Load Balancer Controller / ALB) para roteamento L7, TLS e path-based routing.
  Não é obrigatório para o MVP.
  ```

- [ ] **Step 9: verificações estáticas (Git Bash, sem kubectl).**

  ```bash
  cd "C:/Users/thiago/source/repos/Fiap/fiap_15SOAT_fase1"

  # 1) apiVersions/kinds corretos
  grep -rn "apiVersion:\|^kind:" k8s/*.yaml

  # 2) selector == labels (Deployment/Service/HPA/Job usam app: oficina-api)
  grep -rn "app: oficina-api" k8s/deployment.yaml k8s/service.yaml k8s/hpa.yaml k8s/migration-job.yaml

  # 3) namespace consistente
  grep -rn "namespace: oficina" k8s/*.yaml

  # 4) chaves de env batem com o que a app lê
  grep -rn "ConnectionStrings__Default\|Jwt__Secret\|AdminBootstrap__Password\|Webhook__Token" k8s/secret.yaml
  grep -rn "Jwt__Issuer\|Jwt__Audience\|Bootstrap__ExecutarNoStartup\|ASPNETCORE_ENVIRONMENT" k8s/configmap.yaml

  # 5) Job usa o modo migrate
  grep -n "migrate" k8s/migration-job.yaml

  # 6) porta/health coerentes
  grep -rn "containerPort: 8080\|targetPort: 8080\|path: /health\|port: 8080" k8s/deployment.yaml k8s/service.yaml

  # 7) nenhum segredo real vazado (só placeholders)
  grep -rn "TROCAR_NO_DEPLOY\|TROCAR_ENDPOINT_RDS" k8s/secret.yaml
  ```

  Checklist manual esperado:
  - `apps/v1` Deployment · `batch/v1` Job · `autoscaling/v2` HPA · `v1` Service/ConfigMap/Secret/Namespace. ✔
  - `selector.matchLabels.app` (Deployment) e `Service.spec.selector.app` == `template ... labels.app` == `oficina-api`. ✔
  - HPA `scaleTargetRef` aponta para `apps/v1` / `Deployment` / `oficina-api`. ✔
  - Chaves sensíveis SÓ no Secret; não-sensíveis SÓ no ConfigMap; sem duplicação/colisão. ✔
  - `secret.yaml` só tem placeholders. ✔

- [ ] **Step 10: commit da Task 2** (mensagem pt-BR).

  ```bash
  cd "C:/Users/thiago/source/repos/Fiap/fiap_15SOAT_fase1"
  git add k8s/
  git commit -m "$(cat <<'EOF'
  feat(k8s): manifestos de orquestracao da API (Deployment, Service, HPA, Job de migracao)

  Adiciona k8s/ com Namespace, ConfigMap (config nao-sensivel + ExecutarNoStartup=false),
  Secret-template (placeholders; secret real vem do CI/Terraform), Deployment (2 replicas,
  probes /health, resources requests/limits, securityContext restritivo, rootfs read-only
  + emptyDir /tmp), Service LoadBalancer 80->8080, HPA autoscaling/v2 (2-10, CPU 60% / mem 70%)
  e Job oficina-migrate (modo migrate, aplicado antes do rollout). README documenta a ordem
  de apply e as dependencias do Terraform (RDS, metrics-server, ECR).

  Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
  EOF
  )"
  ```

---

## Auto-revisão (consistência) — corrigida inline

- **Consistência de env (configmap × secret × deployment × job):** a app lê `ConnectionStrings:Default`, `Jwt:Secret/Issuer/Audience`, `AdminBootstrap:Password`, `Webhook:Token`, `Bootstrap:ExecutarNoStartup`, `ASPNETCORE_ENVIRONMENT`. Mapa (env com `__`): **ConfigMap** = `ASPNETCORE_ENVIRONMENT`, `Jwt__Issuer`, `Jwt__Audience`, `Bootstrap__ExecutarNoStartup`; **Secret** = `Jwt__Secret`, `ConnectionStrings__Default`, `AdminBootstrap__Password`, `Webhook__Token`. Sem colisão; todas as chaves que o código lê estão cobertas. `ASPNETCORE_URLS` vem do Dockerfile (não precisa no manifesto). ✔
- **A connection string contém senha** → corretamente colocada no **Secret** (não no ConfigMap). O ConfigMap não repete host/porta/db (evita duas fontes de verdade); documentado que o endpoint do RDS entra na criação do Secret pelo CI. ✔
- **selector == labels:** Deployment `selector.matchLabels.app: oficina-api` == pod template `labels.app: oficina-api`; Service `selector.app: oficina-api`; HPA aponta ao Deployment `oficina-api`. ✔
- **Flag de migração coerente entre código e manifests:** código default `true` (compose/local/testes migram no boot); ConfigMap seta `"false"` (pods não migram); Job roda `command: ["dotnet","Oficina.Api.dll","migrate"]` que aciona o ramo migrate do `Program.cs` (`args.Contains("migrate")`) → `InicializadorBanco.ExecutarAsync` (migra+bootstrap) independentemente da flag, pois o HostedService não inicia nesse modo. ✔
- **Nenhum segredo real commitado:** `secret.yaml` usa `stringData` com `TROCAR_NO_DEPLOY`/`TROCAR_ENDPOINT_RDS` e comentário explícito de criação via CI. ✔
- **Testes de integração preservados:** `AuthFixture` não seta `Bootstrap__ExecutarNoStartup` → default `true` → migra no startup → `BootstrapAdminTestes` continua válido. `public partial class Program` mantida. ✔
- **Captive dependency:** `IInicializadorBanco` registrado como **Singleton** (stateless), injetado no HostedService singleton — sem dependência scoped capturada; o escopo é criado dentro de `ExecutarAsync`. ✔
- **Novo projeto de teste no CI:** `dotnet sln add` garante que o `dotnet test` da solution (CI) enumere `Oficina.Infraestrutura.Testes`; é unit test puro (sem Docker), então roda também no gate local. ✔
- **`readOnlyRootFilesystem: true`:** exige `/tmp` gravável → `emptyDir` montado em `/tmp` no Deployment **e** no Job. JWT HS256 dispensa DataProtection persistente. ✔

## Riscos / ambiguidades a revisar

1. **`command` vs `args` no Job:** o plano usa `command: ["dotnet","Oficina.Api.dll","migrate"]` (sobrescreve o ENTRYPOINT). Alternativa equivalente: `args: ["migrate"]` (mantém o ENTRYPOINT) ou `env: STARTUP_TASK=migrate`. O `Program.cs` aceita as três formas. Confirmar preferência do time.
2. **`readOnlyRootFilesystem` + .NET:** se algum componente tentar escrever fora de `/tmp` (ex.: DataProtection em `~/.aspnet`), pode aparecer warning. Como não há cookies/antiforgery e o JWT é HS256, não deve falhar — validar no primeiro deploy (Plano 09) e, se necessário, montar `emptyDir` extra no home do usuário `app`.
3. **RateLimiting em Production:** `AdicionarRateLimit` lê config `RateLimit:*` — o compose funciona sem setá-las (defaults). Confirmar que os defaults valem em Production; se não, adicionar chaves ao ConfigMap (fora do escopo desta task).
4. **CI dispara em `main`/`develop`, não em `fase-2`:** o novo projeto de teste só será exercido pelo pipeline após merge. Localmente ele roda no gate da Task 1.
5. **`Service type: LoadBalancer`** provisiona um ELB por serviço (custo/limites AWS). Ingress/ALB (nota opcional no README) pode ser preferível — decisão do Plano 09.
6. **Substituição de `${ECR_REPOSITORY}:${IMAGE_TAG}`:** depende do CI (envsubst/sed/kustomize). Um `kubectl apply` cru do arquivo falharia por imagem inválida — comportamento esperado; a substituição é responsabilidade do pipeline (Plano 09).
