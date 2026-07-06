# Fase 2 — Plano 05: Refactor Clean Architecture — contexto Auth

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Refatorar o contexto **Auth** (Usuario + login + bootstrap do admin) ponta-a-ponta para a Clean Architecture do curso, replicando o padrão já aprovado em Catálogo (Plano 01), Clientes (Plano 02), Estoque (Plano 03) e OrdensServico (Plano 04): Entidades → Casos de Uso (que passam a consumir `IUsuarioGateway`) → Adaptadores de Interface (`AutenticacaoController` + `UsuarioGateway`) → Frameworks & Drivers (`UsuarioDataSource` EF Core em Infraestrutura + `AuthController` HTTP fino). É o **último** plano do refactor. O comportamento externo (contrato HTTP `/api/v1/auth/login`, rate limiting, bootstrap do admin) **não muda**.

**Architecture:** Os 2 casos de uso de Auth (`LoginUseCase`, `BootstrapAdminUseCase`) passam a depender de `IUsuarioGateway` (definida na camada de Casos de Uso, em `Oficina.Aplicacao`). `LoginUseCase` continua também consumindo `IGeradorTokenJwt` (porta compartilhada em `Oficina.Dominio.ServicosCompartilhados`, **inalterada**) e continua retornando o union `ResultadoLogin` (`Sucesso(LoginResponse)`/`CredenciaisInvalidas`/`UsuarioInativo`) — o `LoginResponse` já é montado dentro da variante `Sucesso` pelo próprio use case. Um `AutenticacaoController` (Adaptadores) — nome distinto do HTTP `AuthController` para evitar colisão de tipo, exatamente como `OrdemDeServicoController` (Adaptadores) vs. `OrdensServicoController` (Api) — orquestra o `LoginUseCase` e devolve o union. `UsuarioGateway` implementa `IUsuarioGateway` delegando 1:1 para `IUsuarioDataSource`; `UsuarioDataSource` (EF Core, em Infraestrutura) implementa `IUsuarioDataSource` com o corpo do antigo `UsuarioRepositorio` **exatamente** (preserva a comparação por `Username` via converter — `u.Username == username` — e não `.Valor`, que não é traduzível no SQL). O controller HTTP `AuthController` fica fino: valida o corpo (400), chama `AutenticacaoController.LoginAsync` e faz o `switch` do union para o status HTTP (200/401/403). **Não há Presenter em Auth** (desvio consciente vs. os outros contextos: o mapeamento union→status HTTP é preocupação exclusivamente HTTP e fica no `AuthController`; o `LoginResponse` já vem pronto na variante `Sucesso`). **Não há cross-context**: só o Auth consome `IUsuarioRepositorio` (confirmado por grep).

**Tech Stack:** C# 12 / .NET 8, ASP.NET Core, EF Core 8 + Npgsql, BCrypt.Net, JWT HS256 (`System.IdentityModel.Tokens.Jwt`), xUnit 2.5.3 + FluentAssertions 6.12.1 + Moq 4.20.72 + `Microsoft.Extensions.Logging.Abstractions` (`NullLogger`), Testcontainers.PostgreSql (integração). O projeto `Oficina.Adaptadores` e `Oficina.Adaptadores.Testes` **já existem** (Planos 01–04) — não há task de scaffold.

## Global Constraints

- **Idioma pt-BR** em código, identificadores, comentários e mensagens de commit (convenção do projeto).
- **.NET 8** (`net8.0`), `Nullable=enable`, `ImplicitUsings=enable` em todos os projetos.
- **Regra de dependência** (curso): `Oficina.Api → Oficina.Adaptadores → Oficina.Aplicacao → Oficina.Dominio`; `Oficina.Infraestrutura → Oficina.Adaptadores` (implementa `IUsuarioDataSource`) + `→ Oficina.Aplicacao` + `→ Oficina.Dominio`; `Oficina.Api → Oficina.Infraestrutura` só para wiring de DI. **`Oficina.Dominio` continua sem dependências externas.** `Oficina.Aplicacao` **não** referencia `Oficina.Adaptadores` — por isso `IUsuarioGateway` mora em `Oficina.Aplicacao`.
- **`IGeradorTokenJwt` PERMANECE** em `Oficina.Dominio.ServicosCompartilhados` (é uma porta já consumida pelo `LoginUseCase`). A implementação `GeradorTokenJwt` na Infra e o registro `AddSingleton<IGeradorTokenJwt, GeradorTokenJwt>()` em `DependencyInjectionAuth` **NÃO mudam**. NÃO transformar isso em gateway.
- **`BootstrapAdminHostedService` (Frameworks & Drivers) NÃO muda** — continua resolvendo `BootstrapAdminUseCase` por DI num escopo e chamando `db.Database.MigrateAsync(...)` antes do bootstrap.
- **DI idiomático**: classes stateless registradas no container (Scoped, exceto o gerador de token que é Singleton e não muda), sem `new`/`static` manual de dependências (exceto nos testes unitários).
- **Contrato HTTP inalterado**: rota `POST /api/v1/auth/login`; corpo `LoginRequest`; resposta `LoginResponse` em 200; 400 quando `Username`/`Password` ausentes; 401 credenciais inválidas; 403 usuário inativo; 429 pelo rate limiting (`EnableRateLimiting(ConfiguracaoRateLimit.PoliticaLogin)` — 5 tentativas / 15 min por IP). Bootstrap do admin cria `admin` com `PrecisaTrocarSenha=true`. Nenhuma exceção é tratada no `AutenticacaoController` nem no HTTP controller além do `switch` do union.
- **Docker indisponível no ambiente local** → **NÃO** rodar os testes de integração (`Oficina.Integracao.Testes`); eles rodam no CI. Gate local = `dotnet build Oficina.sln` (0 erros) + os 3 projetos de teste unitários (Domínio, Aplicação, Adaptadores) verdes.
- **Toolchain .NET 10** no ambiente: rodar **um projeto de teste por chamada** de `dotnet test` (passar múltiplos projetos falha com `MSB1008`).
- **Bash tool = Git Bash** (POSIX sh): usar `rm`, `grep` e heredoc `<<'EOF'` — **não** usar sintaxe PowerShell.
- **`grep -rn "IUsuarioRepositorio" src tests --include=*.cs` deve ficar vazio ao fim** do plano; o build confirma que não há referências pendentes.
- **Cobertura de linha ≥ 80%** no CI (gate). DTO/Request/Response já são excluídos por `coverlet.runsettings`.
- **Branch:** `fase-2`. Commits em pt-BR terminando com:
  `Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>`

## Estrutura de arquivos (o que este plano cria/modifica/deleta)

```
src/Oficina.Aplicacao/
  Auth/Gateways/IUsuarioGateway.cs                           ★NOVO (abstração consumida pelos use cases)
  Auth/LoginUseCase.cs                                       MODIFICADO (IUsuarioRepositorio → IUsuarioGateway)
  Auth/BootstrapAdminUseCase.cs                              MODIFICADO (IUsuarioRepositorio → IUsuarioGateway)

src/Oficina.Adaptadores/
  Auth/DataSources/IUsuarioDataSource.cs                     ★NOVO (interface consumida pelo Gateway)
  Auth/Gateways/UsuarioGateway.cs                            ★NOVO (IUsuarioGateway → delega p/ IUsuarioDataSource)
  Auth/Controllers/AutenticacaoController.cs                 ★NOVO (orquestra LoginUseCase; retorna ResultadoLogin)
  DependencyInjectionAdaptadores.cs                          MODIFICADO (+ IUsuarioGateway e AutenticacaoController)

src/Oficina.Dominio/
  Auth/IUsuarioRepositorio.cs                                DELETADO (vira IUsuarioGateway)

src/Oficina.Infraestrutura/
  Persistencia/DataSources/UsuarioDataSource.cs              ★NOVO (EF Core; corpo do antigo repositório)
  Persistencia/Repositorios/UsuarioRepositorio.cs            DELETADO
  DependencyInjectionRepositorios.cs                         MODIFICADO (troca binding Usuario)

src/Oficina.Api/
  Controllers/AuthController.cs                              MODIFICADO (fino; delega ao AutenticacaoController e faz o switch do union)

tests/Oficina.Adaptadores.Testes/
  Auth/UsuarioGatewayTestes.cs                               ★NOVO (delegação p/ DataSource)
  Auth/AutenticacaoControllerTestes.cs                       ★NOVO (orquestra LoginUseCase; asserts no union)

tests/Oficina.Aplicacao.Testes/
  Auth/LoginUseCaseTestes.cs                                 MODIFICADO (mock IUsuarioGateway)
  Auth/BootstrapAdminUseCaseTestes.cs                        MODIFICADO (mock IUsuarioGateway)
```

**Resumo:** 5 arquivos criados, 7 modificados, 2 deletados. (Não há `MapeadorAuth` a deletar; `IGeradorTokenJwt`/`GeradorTokenJwt`/`DependencyInjectionAuth`/`BootstrapAdminHostedService`/`JwtOptions` **não mudam**.)

**Inalterados (referência — NÃO tocar):**
- `src/Oficina.Dominio/ServicosCompartilhados/IGeradorTokenJwt.cs` (porta compartilhada).
- `src/Oficina.Dominio/Auth/{Usuario,Senha,Username,Perfil,ExcecoesAuth}.cs` (entidade + VOs + exceções).
- `src/Oficina.Aplicacao/Auth/{LoginRequest,LoginResponse,ResultadoLogin}.cs` (DTOs + union).
- `src/Oficina.Infraestrutura/Auth/{GeradorTokenJwt,JwtOptions,BootstrapAdminHostedService,DependencyInjectionAuth}.cs`.
- `src/Oficina.Aplicacao/DependencyInjectionAplicacao.cs` (já registra `LoginUseCase` e `BootstrapAdminUseCase` como Scoped — não muda).

---

## Task 1: Refatorar o contexto Auth para Clean Architecture

Refatora Auth ponta-a-ponta. Muitos passos pequenos; o build só fica verde ao final (a deleção de `IUsuarioRepositorio` quebra os use cases até que gateway, datasource e DI estejam prontos). Ao final: **build verde + os 3 projetos de teste unitários verdes**, com o contrato HTTP intacto (validado no CI pelos testes de integração de Auth).

**Files:**
- Create: `src/Oficina.Aplicacao/Auth/Gateways/IUsuarioGateway.cs`
- Create: `src/Oficina.Adaptadores/Auth/DataSources/IUsuarioDataSource.cs`
- Create: `src/Oficina.Adaptadores/Auth/Gateways/UsuarioGateway.cs`
- Create: `src/Oficina.Adaptadores/Auth/Controllers/AutenticacaoController.cs`
- Create: `src/Oficina.Infraestrutura/Persistencia/DataSources/UsuarioDataSource.cs`
- Create: `tests/Oficina.Adaptadores.Testes/Auth/UsuarioGatewayTestes.cs`
- Create: `tests/Oficina.Adaptadores.Testes/Auth/AutenticacaoControllerTestes.cs`
- Modify: `src/Oficina.Aplicacao/Auth/LoginUseCase.cs`, `src/Oficina.Aplicacao/Auth/BootstrapAdminUseCase.cs`
- Modify: `src/Oficina.Api/Controllers/AuthController.cs`
- Modify: `src/Oficina.Infraestrutura/DependencyInjectionRepositorios.cs`
- Modify: `src/Oficina.Adaptadores/DependencyInjectionAdaptadores.cs`
- Modify: `tests/Oficina.Aplicacao.Testes/Auth/LoginUseCaseTestes.cs`, `tests/Oficina.Aplicacao.Testes/Auth/BootstrapAdminUseCaseTestes.cs`
- Delete: `src/Oficina.Dominio/Auth/IUsuarioRepositorio.cs`, `src/Oficina.Infraestrutura/Persistencia/Repositorios/UsuarioRepositorio.cs`

**Interfaces (padrão idêntico aos demais contextos):**
- `Oficina.Aplicacao.Auth.Gateways.IUsuarioGateway` — mesma forma do antigo `IUsuarioRepositorio` (`ObterPorUsernameAsync`, `ExisteAsync`, `AdicionarAsync`, `SalvarAsync`).
- `Oficina.Adaptadores.Auth.DataSources.IUsuarioDataSource` — mesma forma.
- `Oficina.Adaptadores.Auth.Gateways.UsuarioGateway : IUsuarioGateway` — delega 1:1.
- `Oficina.Adaptadores.Auth.Controllers.AutenticacaoController` — `LoginAsync(LoginRequest, CancellationToken) : Task<ResultadoLogin>` (retorna o union; sem Presenter).
- `LoginUseCase` e `BootstrapAdminUseCase` mantêm suas assinaturas públicas (`ExecutarAsync`) e contratos de retorno.

---

- [ ] **Step 1: Criar `IUsuarioGateway` (camada de Casos de Uso)**

Create `src/Oficina.Aplicacao/Auth/Gateways/IUsuarioGateway.cs`:
```csharp
using Oficina.Dominio.Auth;

namespace Oficina.Aplicacao.Auth.Gateways;

public interface IUsuarioGateway
{
    Task<Usuario?> ObterPorUsernameAsync(Username username, CancellationToken cancellationToken);
    Task<bool> ExisteAsync(Username username, CancellationToken cancellationToken);
    Task AdicionarAsync(Usuario usuario, CancellationToken cancellationToken);
    Task SalvarAsync(CancellationToken cancellationToken);
}
```

- [ ] **Step 2: Refatorar os 2 casos de uso de Auth (depender do gateway)**

Substituir `src/Oficina.Aplicacao/Auth/LoginUseCase.cs` (troca `IUsuarioRepositorio` por `IUsuarioGateway`; **mantém** `IGeradorTokenJwt`, o `ILogger`, o union `ResultadoLogin` e a montagem do `LoginResponse` na variante `Sucesso`; o campo passa a se chamar `_gateway`):
```csharp
using Microsoft.Extensions.Logging;
using Oficina.Aplicacao.Auth.Gateways;
using Oficina.Dominio.Auth;
using Oficina.Dominio.ServicosCompartilhados;

namespace Oficina.Aplicacao.Auth;

public class LoginUseCase
{
    private readonly IUsuarioGateway _gateway;
    private readonly IGeradorTokenJwt _gerador;
    private readonly ILogger<LoginUseCase> _log;

    public LoginUseCase(
        IUsuarioGateway gateway,
        IGeradorTokenJwt gerador,
        ILogger<LoginUseCase> log)
    {
        _gateway = gateway;
        _gerador = gerador;
        _log = log;
    }

    public async Task<ResultadoLogin> ExecutarAsync(LoginRequest req, CancellationToken ct)
    {
        Username username;
        try
        {
            username = Username.Criar(req.Username);
        }
        catch (ArgumentException)
        {
            // username inválido — não vaza qual foi o erro
            return new ResultadoLogin.CredenciaisInvalidas();
        }

        var usuario = await _gateway.ObterPorUsernameAsync(username, ct);
        if (usuario is null)
        {
            _log.LogWarning("Tentativa de login com username inexistente.");
            return new ResultadoLogin.CredenciaisInvalidas();
        }

        if (!usuario.Ativo)
            return new ResultadoLogin.UsuarioInativo();

        if (!usuario.Autenticar(req.Password))
        {
            _log.LogWarning("Tentativa de login falhou para {Username}.", username.Valor);
            return new ResultadoLogin.CredenciaisInvalidas();
        }

        var token = _gerador.Gerar(usuario);
        return new ResultadoLogin.Sucesso(
            new LoginResponse(token.AccessToken, token.ExpiraEmSegundos, usuario.PrecisaTrocarSenha));
    }
}
```

Substituir `src/Oficina.Aplicacao/Auth/BootstrapAdminUseCase.cs` (troca `IUsuarioRepositorio` por `IUsuarioGateway`; **mantém** o `ILogger`, a validação da senha inicial, `Usuario.CriarParaBootstrap` e o no-op quando o admin já existe; o campo passa a se chamar `_gateway`):
```csharp
using Microsoft.Extensions.Logging;
using Oficina.Aplicacao.Auth.Gateways;
using Oficina.Dominio.Auth;

namespace Oficina.Aplicacao.Auth;

public class BootstrapAdminUseCase
{
    private readonly IUsuarioGateway _gateway;
    private readonly ILogger<BootstrapAdminUseCase> _log;

    public BootstrapAdminUseCase(IUsuarioGateway gateway, ILogger<BootstrapAdminUseCase> log)
    {
        _gateway = gateway;
        _log = log;
    }

    public async Task ExecutarAsync(string senhaInicial, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(senhaInicial))
            throw new InvalidOperationException(
                "ADMIN_BOOTSTRAP_PASSWORD não configurada — não é possível bootstrap.");

        var username = Username.Criar("admin");

        if (await _gateway.ExisteAsync(username, ct))
        {
            _log.LogInformation("Usuário admin já existe — bootstrap ignorado.");
            return;
        }

        var usuario = Usuario.CriarParaBootstrap(username, Senha.DeTextoPuro(senhaInicial));
        await _gateway.AdicionarAsync(usuario, ct);
        await _gateway.SalvarAsync(ct);

        _log.LogWarning("Usuário admin criado pelo bootstrap. Troca de senha exigida no primeiro login.");
    }
}
```

> Nota: `BootstrapAdminHostedService` (Infra) resolve `BootstrapAdminUseCase` por DI e não referencia mais nada do use case por tipo — não muda.

- [ ] **Step 3: Criar `IUsuarioDataSource` e `UsuarioGateway` (Adaptadores)**

Create `src/Oficina.Adaptadores/Auth/DataSources/IUsuarioDataSource.cs`:
```csharp
using Oficina.Dominio.Auth;

namespace Oficina.Adaptadores.Auth.DataSources;

public interface IUsuarioDataSource
{
    Task<Usuario?> ObterPorUsernameAsync(Username username, CancellationToken cancellationToken);
    Task<bool> ExisteAsync(Username username, CancellationToken cancellationToken);
    Task AdicionarAsync(Usuario usuario, CancellationToken cancellationToken);
    Task SalvarAsync(CancellationToken cancellationToken);
}
```

Create `src/Oficina.Adaptadores/Auth/Gateways/UsuarioGateway.cs`:
```csharp
using Oficina.Adaptadores.Auth.DataSources;
using Oficina.Aplicacao.Auth.Gateways;
using Oficina.Dominio.Auth;

namespace Oficina.Adaptadores.Auth.Gateways;

public class UsuarioGateway : IUsuarioGateway
{
    private readonly IUsuarioDataSource _dataSource;
    public UsuarioGateway(IUsuarioDataSource dataSource) => _dataSource = dataSource;

    public Task<Usuario?> ObterPorUsernameAsync(Username username, CancellationToken ct) =>
        _dataSource.ObterPorUsernameAsync(username, ct);

    public Task<bool> ExisteAsync(Username username, CancellationToken ct) =>
        _dataSource.ExisteAsync(username, ct);

    public Task AdicionarAsync(Usuario usuario, CancellationToken ct) =>
        _dataSource.AdicionarAsync(usuario, ct);

    public Task SalvarAsync(CancellationToken ct) =>
        _dataSource.SalvarAsync(ct);
}
```

- [ ] **Step 4: Criar o `AutenticacaoController` de aplicação (Adaptadores)**

O nome `AutenticacaoController` é **distinto** do HTTP `AuthController` (evita colisão de tipo, como `OrdemDeServicoController` vs. `OrdensServicoController`). Ele apenas orquestra o `LoginUseCase` e devolve o union — **sem Presenter**, porque o `LoginResponse` já vem montado na variante `Sucesso` e o mapeamento union→status HTTP é preocupação exclusivamente HTTP.

Create `src/Oficina.Adaptadores/Auth/Controllers/AutenticacaoController.cs`:
```csharp
using Oficina.Aplicacao.Auth;

namespace Oficina.Adaptadores.Auth.Controllers;

// Controller de aplicação (Adaptadores de Interface): orquestra o caso de uso de login
// e devolve o union ResultadoLogin. NÃO há Presenter em Auth (desvio consciente vs. os
// demais contextos): o LoginResponse já é montado pelo LoginUseCase na variante Sucesso e
// o mapeamento union→status HTTP (200/401/403) fica no AuthController (camada HTTP).
public class AutenticacaoController
{
    private readonly LoginUseCase _login;

    public AutenticacaoController(LoginUseCase login) => _login = login;

    public Task<ResultadoLogin> LoginAsync(LoginRequest req, CancellationToken ct) =>
        _login.ExecutarAsync(req, ct);
}
```

- [ ] **Step 5: Criar o `UsuarioDataSource` (Infraestrutura) com o corpo do antigo repositório**

**CRÍTICO:** copiar o corpo do antigo `UsuarioRepositorio` **exatamente** — em especial a comparação `u.Username == username` (o `Username` é mapeado com `HasConversion`; o EF traduz a comparação direta pelo converter; acessar `.Valor` não é traduzível porque a propriedade some no SQL). `OficinaDbContext` mora em `Oficina.Infraestrutura.Persistencia` (namespace pai do de DataSources), então não precisa de `using` explícito — igual aos demais `*DataSource` da Infra.

Create `src/Oficina.Infraestrutura/Persistencia/DataSources/UsuarioDataSource.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using Oficina.Adaptadores.Auth.DataSources;
using Oficina.Dominio.Auth;

namespace Oficina.Infraestrutura.Persistencia.DataSources;

public class UsuarioDataSource : IUsuarioDataSource
{
    private readonly OficinaDbContext _db;

    public UsuarioDataSource(OficinaDbContext db) => _db = db;

    public Task<Usuario?> ObterPorUsernameAsync(Username username, CancellationToken ct)
    {
        // Username é mapeado com HasConversion: EF traduz a comparação direta
        // u.Username == username usando o converter (string ↔ Username).
        // Acessar .Valor não é traduzível porque a propriedade some no SQL.
        return _db.Usuarios.FirstOrDefaultAsync(u => u.Username == username, ct);
    }

    public Task<bool> ExisteAsync(Username username, CancellationToken ct)
    {
        return _db.Usuarios.AnyAsync(u => u.Username == username, ct);
    }

    public async Task AdicionarAsync(Usuario usuario, CancellationToken ct)
    {
        await _db.Usuarios.AddAsync(usuario, ct);
    }

    public Task SalvarAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);
}
```

- [ ] **Step 6: Deletar o antigo repositório e a antiga interface de repositório de Auth**

Run:
```bash
cd "C:/Users/thiago/source/repos/Fiap/fiap_15SOAT_fase1"
rm src/Oficina.Infraestrutura/Persistencia/Repositorios/UsuarioRepositorio.cs
rm src/Oficina.Dominio/Auth/IUsuarioRepositorio.cs
```

> Após esta deleção, a pasta `src/Oficina.Infraestrutura/Persistencia/Repositorios/` fica vazia (era o último repositório — Catálogo/Clientes/Estoque/OrdensServico já migraram para DataSources). O git não versiona pasta vazia; nada mais a fazer. O build só voltará a compilar após o Step 7 (DI) resolver os bindings.

- [ ] **Step 7: Ajustar o wiring de DI (Infra e Adaptadores)**

Substituir `src/Oficina.Infraestrutura/DependencyInjectionRepositorios.cs` (remove o binding `IUsuarioRepositorio`/`UsuarioRepositorio` e os `using` que só o serviam — `Oficina.Dominio.Auth` e `Oficina.Infraestrutura.Persistencia.Repositorios`, agora sem uso; adiciona `IUsuarioDataSource` + o `using Oficina.Adaptadores.Auth.DataSources`):
```csharp
using Microsoft.Extensions.DependencyInjection;
using Oficina.Adaptadores.Auth.DataSources;
using Oficina.Adaptadores.Catalogo.DataSources;
using Oficina.Adaptadores.Clientes.DataSources;
using Oficina.Adaptadores.Estoque.DataSources;
using Oficina.Adaptadores.OrdensServico.DataSources;
using Oficina.Infraestrutura.Persistencia.DataSources;

namespace Oficina.Infraestrutura;

public static class DependencyInjectionRepositorios
{
    public static IServiceCollection AdicionarRepositorios(this IServiceCollection services)
    {
        // DataSources (Clean Architecture — Frameworks & Drivers)
        services.AddScoped<IUsuarioDataSource, UsuarioDataSource>();
        services.AddScoped<IServicoDataSource, ServicoDataSource>();
        services.AddScoped<IClienteDataSource, ClienteDataSource>();
        services.AddScoped<IPecaDataSource, PecaDataSource>();
        services.AddScoped<IOrdemDeServicoDataSource, OrdemDeServicoDataSource>();
        return services;
    }
}
```

Substituir `src/Oficina.Adaptadores/DependencyInjectionAdaptadores.cs` (adiciona o Gateway e o Controller de Auth):
```csharp
using Microsoft.Extensions.DependencyInjection;
using Oficina.Adaptadores.Auth.Controllers;
using Oficina.Adaptadores.Auth.Gateways;
using Oficina.Adaptadores.Catalogo.Controllers;
using Oficina.Adaptadores.Catalogo.Gateways;
using Oficina.Adaptadores.Clientes.Controllers;
using Oficina.Adaptadores.Clientes.Gateways;
using Oficina.Adaptadores.Estoque.Controllers;
using Oficina.Adaptadores.Estoque.Gateways;
using Oficina.Adaptadores.OrdensServico.Controllers;
using Oficina.Adaptadores.OrdensServico.Gateways;
using Oficina.Aplicacao.Auth.Gateways;
using Oficina.Aplicacao.Catalogo.Gateways;
using Oficina.Aplicacao.Clientes.Gateways;
using Oficina.Aplicacao.Estoque.Gateways;
using Oficina.Aplicacao.OrdensServico.Gateways;

namespace Oficina.Adaptadores;

public static class DependencyInjectionAdaptadores
{
    public static IServiceCollection AdicionarAdaptadores(this IServiceCollection services)
    {
        // Auth
        services.AddScoped<IUsuarioGateway, UsuarioGateway>();
        services.AddScoped<AutenticacaoController>();

        // Catálogo
        services.AddScoped<IServicoGateway, ServicoGateway>();
        services.AddScoped<ServicoController>();

        // Clientes
        services.AddScoped<IClienteGateway, ClienteGateway>();
        services.AddScoped<ClienteController>();

        // Estoque
        services.AddScoped<IPecaGateway, PecaGateway>();
        services.AddScoped<PecaController>();

        // Ordens de Serviço
        services.AddScoped<IOrdemDeServicoGateway, OrdemDeServicoGateway>();
        services.AddScoped<OrdemDeServicoController>();
        return services;
    }
}
```

> Nota: `LoginUseCase` e `BootstrapAdminUseCase` já estão registrados (Scoped) em `Oficina.Aplicacao/DependencyInjectionAplicacao.cs` e **não mudam** — o `AutenticacaoController` recebe `LoginUseCase` por injeção; `BootstrapAdminUseCase` continua sendo resolvido pelo `BootstrapAdminHostedService`. Ambos agora resolvem `IUsuarioGateway` (registrado em `AdicionarAdaptadores`), pois `Program.cs` compõe `AdicionarAplicacao()` + `AdicionarAdaptadores()` + `AdicionarInfraestrutura()` no mesmo container. `IGeradorTokenJwt→GeradorTokenJwt` (Singleton) continua registrado em `DependencyInjectionAuth` (inalterado).

- [ ] **Step 8: Deixar o controller HTTP `AuthController` fino (delegando ao `AutenticacaoController`)**

O `AuthController` deixa de injetar `LoginUseCase` no construtor e passa a receber o `AutenticacaoController` por `[FromServices]` na action (padrão dos demais HTTP controllers refatorados). Preserva **exatamente**: a rota `POST /api/v1/auth/login`, o `[EnableRateLimiting(ConfiguracaoRateLimit.PoliticaLogin)]`, os `[ProducesResponseType]`, a validação de corpo (400) e o `switch` do union → status HTTP (200/401/403/500).

Substituir `src/Oficina.Api/Controllers/AuthController.cs`:
```csharp
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Oficina.Adaptadores.Auth.Controllers;
using Oficina.Api.Configuracao;
using Oficina.Aplicacao.Auth;

namespace Oficina.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    [HttpPost("login")]
    [EnableRateLimiting(ConfiguracaoRateLimit.PoliticaLogin)]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest req,
        [FromServices] AutenticacaoController controller,
        CancellationToken ct)
    {
        if (req is null || string.IsNullOrWhiteSpace(req.Username) || string.IsNullOrWhiteSpace(req.Password))
            return BadRequest(new { erro = "Username e password são obrigatórios." });

        var resultado = await controller.LoginAsync(req, ct);
        return resultado switch
        {
            ResultadoLogin.Sucesso s => Ok(s.Response),
            ResultadoLogin.CredenciaisInvalidas => Unauthorized(new { erro = "Credenciais inválidas." }),
            ResultadoLogin.UsuarioInativo => StatusCode(StatusCodes.Status403Forbidden, new { erro = "Usuário inativo." }),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }
}
```

- [ ] **Step 9: Adaptar os testes de use case de Auth (mock `IUsuarioGateway`)**

Substituir `tests/Oficina.Aplicacao.Testes/Auth/LoginUseCaseTestes.cs` (troca `Mock<IUsuarioRepositorio>` por `Mock<IUsuarioGateway>` + `using Oficina.Aplicacao.Auth.Gateways;`; renomeia o campo para `_gateway`; nenhum cenário muda — todos os métodos usados (`ObterPorUsernameAsync`) existem em `IUsuarioGateway`):
```csharp
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Oficina.Aplicacao.Auth;
using Oficina.Aplicacao.Auth.Gateways;
using Oficina.Dominio.Auth;
using Oficina.Dominio.ServicosCompartilhados;
using Xunit;

namespace Oficina.Aplicacao.Testes.Auth;

public class LoginUseCaseTestes
{
    private readonly Mock<IUsuarioGateway> _gateway = new();
    private readonly Mock<IGeradorTokenJwt> _gerador = new();

    private LoginUseCase Construir() =>
        new(_gateway.Object, _gerador.Object, NullLogger<LoginUseCase>.Instance);

    [Fact]
    public async Task Executar_ComCredenciaisCorretas_DeveRetornarSucesso()
    {
        var senha = Senha.DeTextoPuro("AlteraMe@123");
        var u = Usuario.Criar(Username.Criar("admin"), senha, Perfil.Admin);

        _gateway.Setup(r => r.ObterPorUsernameAsync(
                It.Is<Username>(x => x.Valor == "admin"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(u);

        _gerador.Setup(g => g.Gerar(u)).Returns(new TokenJwt("tok", 3600));

        var resultado = await Construir().ExecutarAsync(
            new LoginRequest("admin", "AlteraMe@123"), default);

        resultado.Should().BeOfType<ResultadoLogin.Sucesso>()
            .Which.Response.AccessToken.Should().Be("tok");
    }

    [Fact]
    public async Task Executar_ComUsuarioInexistente_DeveRetornarCredenciaisInvalidas()
    {
        _gateway.Setup(r => r.ObterPorUsernameAsync(It.IsAny<Username>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Usuario?)null);

        var resultado = await Construir().ExecutarAsync(
            new LoginRequest("naoexiste", "qualquer8"), default);

        resultado.Should().BeOfType<ResultadoLogin.CredenciaisInvalidas>();
    }

    [Fact]
    public async Task Executar_ComSenhaErrada_DeveRetornarCredenciaisInvalidas()
    {
        var u = Usuario.Criar(Username.Criar("admin"),
            Senha.DeTextoPuro("AlteraMe@123"), Perfil.Admin);

        _gateway.Setup(r => r.ObterPorUsernameAsync(It.IsAny<Username>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(u);

        var resultado = await Construir().ExecutarAsync(
            new LoginRequest("admin", "ErradoXYZ@9"), default);

        resultado.Should().BeOfType<ResultadoLogin.CredenciaisInvalidas>();
    }

    [Fact]
    public async Task Executar_ComUsuarioInativo_DeveRetornarUsuarioInativo()
    {
        var u = Usuario.Criar(Username.Criar("admin"),
            Senha.DeTextoPuro("AlteraMe@123"), Perfil.Admin);
        u.Inativar();

        _gateway.Setup(r => r.ObterPorUsernameAsync(It.IsAny<Username>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(u);

        var resultado = await Construir().ExecutarAsync(
            new LoginRequest("admin", "AlteraMe@123"), default);

        resultado.Should().BeOfType<ResultadoLogin.UsuarioInativo>();
    }

    [Fact]
    public async Task Executar_ComUsernameInvalido_DeveRetornarCredenciaisInvalidas()
    {
        var resultado = await Construir().ExecutarAsync(
            new LoginRequest("us@", "AlteraMe@123"), default);

        resultado.Should().BeOfType<ResultadoLogin.CredenciaisInvalidas>();
    }
}
```

Substituir `tests/Oficina.Aplicacao.Testes/Auth/BootstrapAdminUseCaseTestes.cs` (troca `Mock<IUsuarioRepositorio>` por `Mock<IUsuarioGateway>` + `using Oficina.Aplicacao.Auth.Gateways;`; renomeia o campo para `_gateway`):
```csharp
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Oficina.Aplicacao.Auth;
using Oficina.Aplicacao.Auth.Gateways;
using Oficina.Dominio.Auth;
using Xunit;

namespace Oficina.Aplicacao.Testes.Auth;

public class BootstrapAdminUseCaseTestes
{
    private readonly Mock<IUsuarioGateway> _gateway = new();

    private BootstrapAdminUseCase Construir() =>
        new(_gateway.Object, NullLogger<BootstrapAdminUseCase>.Instance);

    [Fact]
    public async Task Executar_QuandoAdminNaoExiste_DeveCriarComBootstrap()
    {
        _gateway.Setup(r => r.ExisteAsync(It.IsAny<Username>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        await Construir().ExecutarAsync("AlteraMe@123", default);

        _gateway.Verify(r => r.AdicionarAsync(
            It.Is<Usuario>(u => u.Username.Valor == "admin" && u.PrecisaTrocarSenha),
            It.IsAny<CancellationToken>()), Times.Once);
        _gateway.Verify(r => r.SalvarAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Executar_QuandoAdminExiste_DeveSerNoOp()
    {
        _gateway.Setup(r => r.ExisteAsync(It.IsAny<Username>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await Construir().ExecutarAsync("AlteraMe@123", default);

        _gateway.Verify(r => r.AdicionarAsync(It.IsAny<Usuario>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Executar_ComSenhaVazia_DeveLancar()
    {
        var act = async () => await Construir().ExecutarAsync("", default);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*ADMIN_BOOTSTRAP_PASSWORD*");
    }
}
```

- [ ] **Step 10: Escrever o teste do `UsuarioGateway` (delegação para o DataSource)**

Create `tests/Oficina.Adaptadores.Testes/Auth/UsuarioGatewayTestes.cs`:
```csharp
using FluentAssertions;
using Moq;
using Oficina.Adaptadores.Auth.DataSources;
using Oficina.Adaptadores.Auth.Gateways;
using Oficina.Dominio.Auth;

namespace Oficina.Adaptadores.Testes.Auth;

public class UsuarioGatewayTestes
{
    private static Usuario CriarUsuario() =>
        Usuario.Criar(Username.Criar("admin"), Senha.DeTextoPuro("AlteraMe@123"), Perfil.Admin);

    [Fact]
    public async Task ObterPorUsernameAsync_DeveDelegarParaODataSource()
    {
        var ds = new Mock<IUsuarioDataSource>();
        var esperado = CriarUsuario();
        ds.Setup(d => d.ObterPorUsernameAsync(It.IsAny<Username>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(esperado);

        var gateway = new UsuarioGateway(ds.Object);
        var obtido = await gateway.ObterPorUsernameAsync(esperado.Username, default);

        obtido.Should().BeSameAs(esperado);
        ds.Verify(d => d.ObterPorUsernameAsync(esperado.Username, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExisteAsync_DeveDelegarParaODataSource()
    {
        var ds = new Mock<IUsuarioDataSource>();
        ds.Setup(d => d.ExisteAsync(It.IsAny<Username>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var gateway = new UsuarioGateway(ds.Object);
        var existe = await gateway.ExisteAsync(Username.Criar("admin"), default);

        existe.Should().BeTrue();
        ds.Verify(d => d.ExisteAsync(It.IsAny<Username>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AdicionarESalvar_DevemDelegarParaODataSource()
    {
        var ds = new Mock<IUsuarioDataSource>();
        var gateway = new UsuarioGateway(ds.Object);
        var usuario = CriarUsuario();

        await gateway.AdicionarAsync(usuario, default);
        await gateway.SalvarAsync(default);

        ds.Verify(d => d.AdicionarAsync(usuario, It.IsAny<CancellationToken>()), Times.Once);
        ds.Verify(d => d.SalvarAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
```

- [ ] **Step 11: Escrever o teste do `AutenticacaoController` (orquestração do `LoginUseCase`; asserts no union)**

O teste constrói o `AutenticacaoController` com um `LoginUseCase` **real**, injetando `IUsuarioGateway` e `IGeradorTokenJwt` mockados (`NullLogger` para o log). Assim exercita a orquestração real e confirma que o controller devolve o union sem transformá-lo.

Create `tests/Oficina.Adaptadores.Testes/Auth/AutenticacaoControllerTestes.cs`:
```csharp
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Oficina.Adaptadores.Auth.Controllers;
using Oficina.Aplicacao.Auth;
using Oficina.Aplicacao.Auth.Gateways;
using Oficina.Dominio.Auth;
using Oficina.Dominio.ServicosCompartilhados;

namespace Oficina.Adaptadores.Testes.Auth;

public class AutenticacaoControllerTestes
{
    private readonly Mock<IUsuarioGateway> _gateway = new();
    private readonly Mock<IGeradorTokenJwt> _gerador = new();

    private AutenticacaoController CriarController() =>
        new(new LoginUseCase(_gateway.Object, _gerador.Object, NullLogger<LoginUseCase>.Instance));

    [Fact]
    public async Task LoginAsync_ComCredenciaisCorretas_DeveRetornarSucessoComToken()
    {
        var usuario = Usuario.Criar(Username.Criar("admin"),
            Senha.DeTextoPuro("AlteraMe@123"), Perfil.Admin);
        _gateway.Setup(g => g.ObterPorUsernameAsync(It.IsAny<Username>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(usuario);
        _gerador.Setup(g => g.Gerar(usuario)).Returns(new TokenJwt("tok", 3600));

        var resultado = await CriarController()
            .LoginAsync(new LoginRequest("admin", "AlteraMe@123"), default);

        resultado.Should().BeOfType<ResultadoLogin.Sucesso>()
            .Which.Response.AccessToken.Should().Be("tok");
    }

    [Fact]
    public async Task LoginAsync_ComUsuarioInexistente_DeveRetornarCredenciaisInvalidas()
    {
        _gateway.Setup(g => g.ObterPorUsernameAsync(It.IsAny<Username>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Usuario?)null);

        var resultado = await CriarController()
            .LoginAsync(new LoginRequest("naoexiste", "qualquer8"), default);

        resultado.Should().BeOfType<ResultadoLogin.CredenciaisInvalidas>();
    }

    [Fact]
    public async Task LoginAsync_ComUsuarioInativo_DeveRetornarUsuarioInativo()
    {
        var usuario = Usuario.Criar(Username.Criar("admin"),
            Senha.DeTextoPuro("AlteraMe@123"), Perfil.Admin);
        usuario.Inativar();
        _gateway.Setup(g => g.ObterPorUsernameAsync(It.IsAny<Username>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(usuario);

        var resultado = await CriarController()
            .LoginAsync(new LoginRequest("admin", "AlteraMe@123"), default);

        resultado.Should().BeOfType<ResultadoLogin.UsuarioInativo>();
    }
}
```

- [ ] **Step 12: Verificar que não sobraram referências à interface deletada**

Run:
```bash
cd "C:/Users/thiago/source/repos/Fiap/fiap_15SOAT_fase1"
grep -rn "IUsuarioRepositorio" src tests --include=*.cs
```
Expected: **nenhuma linha** de saída (todas as referências migraram para `IUsuarioGateway`/`IUsuarioDataSource`). Se algo aparecer, corrigir a referência remanescente antes de prosseguir. (Não há `MapeadorAuth`, então não há grep adicional.)

- [ ] **Step 13: Compilar a solution**

Run: `dotnet build Oficina.sln`
Expected: **Build succeeded**, 0 erros (todas as referências resolvidas; deleções sem referências pendentes; `AutenticacaoController` e `UsuarioGateway`/`UsuarioDataSource` registrados).

- [ ] **Step 14: Rodar os testes unitários — um projeto por chamada (toolchain .NET 10)**

Run (uma chamada por projeto; passar múltiplos projetos falha com `MSB1008`):
```bash
dotnet test tests/Oficina.Dominio.Testes
dotnet test tests/Oficina.Aplicacao.Testes
dotnet test tests/Oficina.Adaptadores.Testes
```
Expected: PASS em todos (inclui os testes adaptados de Auth em `Oficina.Aplicacao.Testes` e os novos `UsuarioGatewayTestes`/`AutenticacaoControllerTestes` em `Oficina.Adaptadores.Testes`).

- [ ] **Step 15: (CI apenas) Testes de integração — NÃO rodar localmente (Docker indisponível)**

Os testes `tests/Oficina.Integracao.Testes/Auth/LoginEndpointTestes.cs` (contrato HTTP do login: 200/400/401/403, `LoginResponse`), `tests/Oficina.Integracao.Testes/Auth/BootstrapAdminTestes.cs` (bootstrap cria `admin` com `PrecisaTrocarSenha=true`) e `tests/Oficina.Integracao.Testes/Auth/RateLimitTestes.cs` (429 após 5 tentativas) são a rede de segurança do refactor. Eles usam Testcontainers + Postgres e rodam **no CI**. Localmente, apenas registrar que o gate local (Steps 13–14) passou; **não** executar `dotnet test tests/Oficina.Integracao.Testes` sem Docker.

- [ ] **Step 16: Commit**

```bash
cd "C:/Users/thiago/source/repos/Fiap/fiap_15SOAT_fase1"
git add -A
git commit -m "$(cat <<'EOF'
refactor: Auth para Clean Architecture (Gateway/DataSource/Controller)

Casos de uso de Auth (LoginUseCase, BootstrapAdminUseCase) passam a consumir
IUsuarioGateway; AutenticacaoController (aplicacao) orquestra o LoginUseCase e
devolve o union ResultadoLogin; UsuarioGateway delega para IUsuarioDataSource,
implementado por UsuarioDataSource (EF Core) — preserva a comparacao por Username
via converter. Controller HTTP AuthController fica fino (valida corpo e faz o switch
union->status: 200/401/403), sem Presenter (o LoginResponse ja vem na variante
Sucesso). IGeradorTokenJwt permanece porta em Dominio.ServicosCompartilhados e
GeradorTokenJwt/DependencyInjectionAuth/BootstrapAdminHostedService nao mudam.
Remove IUsuarioRepositorio e UsuarioRepositorio. Contrato HTTP inalterado.

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Auto-revisão do plano (cobertura vs. contexto Auth)

- **Cobertura de arquivos:** os 2 casos de uso de Auth (`LoginUseCase`, `BootstrapAdminUseCase`), a interface de domínio (`IUsuarioRepositorio`), o repositório de infra (`UsuarioRepositorio`), o controller HTTP (`AuthController`) e a DI (Infra + Adaptadores) estão todos endereçados. Os DTOs/union (`LoginRequest`, `LoginResponse`, `ResultadoLogin`) e a entidade/VOs (`Usuario`, `Senha`, `Username`, `Perfil`, `ExcecoesAuth`) **não mudam** (contrato e regras idênticos). ✔
- **Particularidades do Auth (desvios conscientes vs. o template Catálogo/Clientes/Estoque/OrdensServico):**
  1. **`IGeradorTokenJwt` permanece porta em `Oficina.Dominio.ServicosCompartilhados`** — NÃO vira gateway. É consumida diretamente pelo `LoginUseCase`; a impl `GeradorTokenJwt` (Infra, `Singleton`) e seu registro em `DependencyInjectionAuth` não mudam. É o único contexto onde uma porta compartilhada de domínio sobrevive ao refactor. ✔
  2. **Sem Presenter** — diferentemente de Catálogo/Clientes/Estoque/OrdensServico (que têm `*Presenter`), Auth não cria Presenter. Motivo: o mapeamento union→status HTTP (200/401/403/500) é preocupação exclusivamente HTTP e fica no `AuthController`; o `LoginResponse` já é montado pelo `LoginUseCase` dentro da variante `Sucesso`. O `AutenticacaoController` apenas devolve o union. **Registrado explicitamente como desvio consciente.** ✔
  3. **Union → status HTTP fica no HTTP controller** — o `switch` sobre `ResultadoLogin` (`Sucesso`→200, `CredenciaisInvalidas`→401, `UsuarioInativo`→403, `_`→500) permanece no `AuthController`, que também mantém a validação de corpo (400) e o `[EnableRateLimiting]`. ✔
  4. **Colisão de nomes** — o controller de aplicação chama-se `AutenticacaoController` (namespace `Oficina.Adaptadores.Auth.Controllers`), distinto do HTTP `AuthController` (`Oficina.Api.Controllers`), exatamente como `OrdemDeServicoController` vs. `OrdensServicoController`. Sem necessidade de alias. ✔
  5. **`BootstrapAdminHostedService` (Frameworks & Drivers) NÃO muda** — continua resolvendo `BootstrapAdminUseCase` por DI num escopo e chamando `MigrateAsync`. O use case agora resolve `IUsuarioGateway` transitivamente. ✔
  6. **Sem cross-context** — o grep inicial confirmou que `IUsuarioRepositorio` só é referenciado dentro do próprio Auth (interface, repositório, 2 use cases, 2 testes, 1 binding de DI). Nenhum outro contexto consome usuários. O Step 12 (grep final vazio) garante que a migração foi completa. ✔
  7. **Comparação por `Username` via converter** — o `UsuarioDataSource` preserva `u.Username == username` (traduzível pelo `HasConversion`) em vez de `.Valor` (não traduzível), com o comentário explicativo do repositório original. Não há paginação, `Include`, transação serializável nem `MarcarComoNovo` neste contexto (Usuario é agregado simples, sem coleção aninhada). ✔
- **Consistência de tipos/nomes entre passos:** assinaturas de `IUsuarioGateway` ≡ `IUsuarioDataSource` (`ObterPorUsernameAsync`/`ExisteAsync`/`AdicionarAsync`/`SalvarAsync`); `LoginUseCase`/`BootstrapAdminUseCase` mantêm `ExecutarAsync` e contratos de retorno (`Task<ResultadoLogin>` / `Task`); o `AutenticacaoController.LoginAsync` devolve `Task<ResultadoLogin>` consumido pelo `switch` do `AuthController`; os testes adaptados apenas trocam o tipo do mock (`IUsuarioRepositorio`→`IUsuarioGateway`) e renomeiam o campo, sem mudar cenários. ✔
- **Sem placeholders:** todo código é completo; caminhos e comandos são absolutos/exatos; comandos de teste respeitam a regra "um projeto por chamada". ✔
- **Regra de dependência:** `IUsuarioGateway` mora em `Oficina.Aplicacao`; `IUsuarioDataSource` em `Oficina.Adaptadores`; `UsuarioDataSource` (Infra) implementa a interface de Adaptadores; `Oficina.Dominio` continua sem dependências externas (mantém apenas `IGeradorTokenJwt` como porta compartilhada). ✔
- **DI:** `LoginUseCase`/`BootstrapAdminUseCase` já registrados em `DependencyInjectionAplicacao.cs` (Scoped) — não mudam; `AdicionarAdaptadores` ganha `IUsuarioGateway→UsuarioGateway` + `AutenticacaoController`; `AdicionarRepositorios` troca `IUsuarioRepositorio→UsuarioRepositorio` por `IUsuarioDataSource→UsuarioDataSource` (e limpa os `using` de `Oficina.Dominio.Auth` e `Oficina.Infraestrutura.Persistencia.Repositorios`, que ficaram sem uso — a pasta `Repositorios/` fica vazia). `IGeradorTokenJwt→GeradorTokenJwt` (Singleton) em `DependencyInjectionAuth` inalterado. ✔
```
