# Fase 2 — Plano 01: Refactor Clean Architecture — scaffold + fatia de referência (Catálogo)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Criar o projeto `Oficina.Adaptadores` e refatorar o contexto **Catálogo** ponta-a-ponta para a Clean Architecture do curso (Entidades → Casos de Uso → Adaptadores: Controllers/Gateways/Presenters → Frameworks & Drivers: DataSource EF + HTTP fino), estabelecendo o **padrão de referência** que os demais contextos vão replicar.

**Architecture:** Casos de uso passam a depender de `IServicoGateway` (definida na camada de Casos de Uso) e a **retornar entidades de domínio**; um `ServicoController` (aplicação) orquestra os casos de uso e usa `ServicoPresenter` para formatar a saída; `ServicoGateway` implementa `IServicoGateway` delegando para `IServicoDataSource`; `ServicoDataSource` (EF Core, em Infraestrutura) implementa `IServicoDataSource`; o controller HTTP `ServicosController` fica fino e só delega para o `ServicoController` de aplicação. Comportamento externo (contrato HTTP) **não muda** — os testes de integração existentes são a rede de segurança.

**Tech Stack:** C# 12 / .NET 8, ASP.NET Core, EF Core 8 + Npgsql, xUnit 2.5.3 + FluentAssertions 6.12.1 + Moq 4.20.72, Testcontainers.PostgreSql (integração).

## Global Constraints

- **Idioma pt-BR** em código, identificadores, comentários e mensagens de commit (convenção do projeto).
- **.NET 8** (`net8.0`), `Nullable=enable`, `ImplicitUsings=enable` em todos os projetos.
- **Regra de dependência** (curso): interno não referencia externo; fluxo de referências `Oficina.Api → Oficina.Adaptadores → Oficina.Aplicacao → Oficina.Dominio`; `Oficina.Infraestrutura → Oficina.Adaptadores` (implementa `IXxxDataSource`) + `→ Oficina.Aplicacao` + `→ Oficina.Dominio`; `Oficina.Api → Oficina.Infraestrutura` só para wiring de DI. **`Oficina.Dominio` continua sem dependências externas.**
- **DI idiomático**: classes stateless registradas no container (Scoped), sem `new`/`static` manual de dependências.
- **Cobertura de linha ≥ 80%** no CI (gate). Novos tipos DTO/Request/Response já são excluídos por `coverlet.runsettings`.
- **Branch:** `fase-2`. Commits em pt-BR terminando com:
  `Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>`
- **Não alterar o contrato HTTP do Catálogo** (rotas, JSON de request/response, status codes). Os testes de integração `ServicosEndpointTestes` devem passar sem edição.

## Estrutura de arquivos (o que este plano cria/modifica)

```
src/Oficina.Adaptadores/                                    ★NOVO projeto (camada Adaptadores de Interface)
  Oficina.Adaptadores.csproj
  DependencyInjectionAdaptadores.cs                         AdicionarAdaptadores() (Controllers + Gateways)
  Catalogo/DataSources/IServicoDataSource.cs                interface consumida pelo Gateway
  Catalogo/Gateways/ServicoGateway.cs                       IServicoGateway → delega p/ IServicoDataSource
  Catalogo/Presenters/ServicoPresenter.cs                   Servico → ServicoResponse / PaginaServicos
  Catalogo/Controllers/ServicoController.cs                 orquestra os use cases + Presenter

src/Oficina.Aplicacao/
  Catalogo/Gateways/IServicoGateway.cs                      ★NOVO (abstração que o use case consome)
  Catalogo/CriarServicoUseCase.cs                           MODIFICADO (depende de gateway; retorna Servico)
  Catalogo/ObterServicoPorIdUseCase.cs                      MODIFICADO (retorna Servico?)
  Catalogo/ListarServicosUseCase.cs                         MODIFICADO (retorna ResultadoListaServicos)
  Catalogo/AtualizarServicoUseCase.cs                       MODIFICADO (retorna Servico?)
  Catalogo/RemoverServicoUseCase.cs                         MODIFICADO (depende de gateway)
  Catalogo/MapeadorServicoResponse.cs                       DELETADO (vira ServicoPresenter)

src/Oficina.Dominio/
  Catalogo/IServicoRepositorio.cs                           DELETADO (vira IServicoGateway)

src/Oficina.Infraestrutura/
  Persistencia/DataSources/ServicoDataSource.cs             ★NOVO (EF Core; corpo do antigo repositório)
  Persistencia/Repositorios/ServicoRepositorio.cs           DELETADO
  DependencyInjectionRepositorios.cs                        MODIFICADO (troca binding Servico)

src/Oficina.Api/
  Controllers/ServicosController.cs                         MODIFICADO (fino; delega ao ServicoController)
  Program.cs                                                MODIFICADO (+ AdicionarAdaptadores())

tests/Oficina.Adaptadores.Testes/                           ★NOVO projeto de teste
  Oficina.Adaptadores.Testes.csproj
  Catalogo/ServicoPresenterTestes.cs
  Catalogo/ServicoGatewayTestes.cs
  Catalogo/ServicoControllerTestes.cs

tests/Oficina.Aplicacao.Testes/
  Catalogo/CriarServicoUseCaseTestes.cs                     MODIFICADO (mock IServicoGateway; assert entidade)
  Catalogo/AtualizarServicoUseCaseTestes.cs                 MODIFICADO (mesma troca)
```

---

## Task 1: Scaffold do projeto `Oficina.Adaptadores` (+ projeto de teste) e wiring vazio

**Files:**
- Create: `src/Oficina.Adaptadores/Oficina.Adaptadores.csproj`
- Create: `src/Oficina.Adaptadores/DependencyInjectionAdaptadores.cs`
- Create: `tests/Oficina.Adaptadores.Testes/Oficina.Adaptadores.Testes.csproj`
- Modify: `Oficina.sln`, `src/Oficina.Api/Oficina.Api.csproj`, `src/Oficina.Infraestrutura/Oficina.Infraestrutura.csproj`, `src/Oficina.Api/Program.cs`

**Interfaces:**
- Produces: `Oficina.Adaptadores.DependencyInjectionAdaptadores.AdicionarAdaptadores(this IServiceCollection) : IServiceCollection` (vazio nesta task; preenchido na Task 2).

- [ ] **Step 1: Criar o projeto de biblioteca e adicionar à solution**

Run:
```bash
cd "C:/Users/thiago/source/repos/Fiap/fiap_15SOAT_fase1"
dotnet new classlib -n Oficina.Adaptadores -o src/Oficina.Adaptadores -f net8.0
rm src/Oficina.Adaptadores/Class1.cs
dotnet sln Oficina.sln add src/Oficina.Adaptadores/Oficina.Adaptadores.csproj
```
Expected: projeto criado e adicionado à solution.

- [ ] **Step 2: Definir o `.csproj` de `Oficina.Adaptadores`**

Substituir o conteúdo de `src/Oficina.Adaptadores/Oficina.Adaptadores.csproj` por:
```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\Oficina.Aplicacao\Oficina.Aplicacao.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="8.0.1" />
  </ItemGroup>

</Project>
```

- [ ] **Step 3: Adicionar as referências de projeto (regra de dependência)**

Run:
```bash
dotnet add src/Oficina.Api/Oficina.Api.csproj reference src/Oficina.Adaptadores/Oficina.Adaptadores.csproj
dotnet add src/Oficina.Infraestrutura/Oficina.Infraestrutura.csproj reference src/Oficina.Adaptadores/Oficina.Adaptadores.csproj
```
Expected: `Oficina.Api` e `Oficina.Infraestrutura` passam a referenciar `Oficina.Adaptadores`.

- [ ] **Step 4: Criar o DI vazio dos Adaptadores**

Create `src/Oficina.Adaptadores/DependencyInjectionAdaptadores.cs`:
```csharp
using Microsoft.Extensions.DependencyInjection;

namespace Oficina.Adaptadores;

public static class DependencyInjectionAdaptadores
{
    public static IServiceCollection AdicionarAdaptadores(this IServiceCollection services)
    {
        // Registros de Controllers de aplicação e Gateways por contexto (preenchidos por contexto).
        return services;
    }
}
```

- [ ] **Step 5: Ligar o DI no `Program.cs`**

Em `src/Oficina.Api/Program.cs`, adicionar o `using` e a chamada. Alterar o bloco de composição (linhas ~52-56) de:
```csharp
builder.Services.AdicionarInfraestrutura(builder.Configuration);
builder.Services.AdicionarAplicacao();
builder.Services.AdicionarJwtBearer(builder.Configuration);
```
para:
```csharp
builder.Services.AdicionarInfraestrutura(builder.Configuration);
builder.Services.AdicionarAplicacao();
builder.Services.AdicionarAdaptadores();
builder.Services.AdicionarJwtBearer(builder.Configuration);
```
E adicionar no topo do arquivo, junto aos demais `using`:
```csharp
using Oficina.Adaptadores;
```

- [ ] **Step 6: Criar o projeto de teste dos Adaptadores**

Run:
```bash
dotnet new xunit -n Oficina.Adaptadores.Testes -o tests/Oficina.Adaptadores.Testes
rm tests/Oficina.Adaptadores.Testes/UnitTest1.cs
dotnet sln Oficina.sln add tests/Oficina.Adaptadores.Testes/Oficina.Adaptadores.Testes.csproj
```

- [ ] **Step 7: Definir o `.csproj` do projeto de teste**

Substituir o conteúdo de `tests/Oficina.Adaptadores.Testes/Oficina.Adaptadores.Testes.csproj` por:
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
    <Using Include="Xunit" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.8.0" />
    <PackageReference Include="xunit" Version="2.5.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.5.3">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
    <PackageReference Include="FluentAssertions" Version="6.12.1" />
    <PackageReference Include="Moq" Version="4.20.72" />
    <PackageReference Include="coverlet.collector" Version="6.0.2">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\Oficina.Adaptadores\Oficina.Adaptadores.csproj" />
  </ItemGroup>

</Project>
```

- [ ] **Step 8: Compilar a solution inteira**

Run: `dotnet build Oficina.sln`
Expected: **Build succeeded**, 0 erros. (O novo projeto está vazio e wired; nada quebrou.)

- [ ] **Step 9: Rodar a suíte de testes de domínio/aplicação (sem Docker) para garantir que nada quebrou**

Run: `dotnet test tests/Oficina.Dominio.Testes tests/Oficina.Aplicacao.Testes`
Expected: PASS (todos verdes).

- [ ] **Step 10: Commit**

```bash
git add -A
git commit -m "$(cat <<'EOF'
refactor: cria projeto Oficina.Adaptadores e liga DI (scaffold Clean Arch)

Novo projeto da camada de Adaptadores de Interface + projeto de teste,
referencias conforme a regra de dependencia e AdicionarAdaptadores() ligado
no Program.cs. Ainda vazio; preenchido por contexto a partir do Catalogo.

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 2: Refatorar o contexto Catálogo para Clean Architecture (fatia de referência)

Refatora o Catálogo ponta-a-ponta. Muitos passos pequenos; ao final, **build verde + todos os testes (incl. integração) passando**, com o contrato HTTP intacto.

**Files:**
- Create: `src/Oficina.Aplicacao/Catalogo/Gateways/IServicoGateway.cs`
- Create: `src/Oficina.Adaptadores/Catalogo/DataSources/IServicoDataSource.cs`
- Create: `src/Oficina.Adaptadores/Catalogo/Gateways/ServicoGateway.cs`
- Create: `src/Oficina.Adaptadores/Catalogo/Presenters/ServicoPresenter.cs`
- Create: `src/Oficina.Adaptadores/Catalogo/Controllers/ServicoController.cs`
- Create: `src/Oficina.Infraestrutura/Persistencia/DataSources/ServicoDataSource.cs`
- Create: `tests/Oficina.Adaptadores.Testes/Catalogo/ServicoPresenterTestes.cs`
- Create: `tests/Oficina.Adaptadores.Testes/Catalogo/ServicoGatewayTestes.cs`
- Create: `tests/Oficina.Adaptadores.Testes/Catalogo/ServicoControllerTestes.cs`
- Modify: os 5 use cases de `src/Oficina.Aplicacao/Catalogo/`
- Modify: `src/Oficina.Api/Controllers/ServicosController.cs`
- Modify: `src/Oficina.Infraestrutura/DependencyInjectionRepositorios.cs`
- Modify: `src/Oficina.Adaptadores/DependencyInjectionAdaptadores.cs`
- Modify: `tests/Oficina.Aplicacao.Testes/Catalogo/CriarServicoUseCaseTestes.cs`, `.../AtualizarServicoUseCaseTestes.cs`
- Delete: `src/Oficina.Dominio/Catalogo/IServicoRepositorio.cs`, `src/Oficina.Infraestrutura/Persistencia/Repositorios/ServicoRepositorio.cs`, `src/Oficina.Aplicacao/Catalogo/MapeadorServicoResponse.cs`

**Interfaces:**
- Produces (padrão a ser replicado nos outros contextos):
  - `Oficina.Aplicacao.Catalogo.Gateways.IServicoGateway` — mesma forma do antigo `IServicoRepositorio`.
  - `Oficina.Adaptadores.Catalogo.DataSources.IServicoDataSource` — mesma forma.
  - `Oficina.Adaptadores.Catalogo.Gateways.ServicoGateway : IServicoGateway`.
  - `Oficina.Adaptadores.Catalogo.Presenters.ServicoPresenter` (estático): `Apresentar(Servico) : ServicoResponse`, `ApresentarPagina(IReadOnlyList<Servico>, int, int, int) : PaginaServicos`.
  - `Oficina.Adaptadores.Catalogo.Controllers.ServicoController` — `CriarAsync`, `ObterPorIdAsync`, `ListarAsync`, `AtualizarAsync`, `RemoverAsync`.
  - Use cases retornam **entidade**: `CriarServicoUseCase.ExecutarAsync → Task<Servico>`, `ObterServicoPorIdUseCase → Task<Servico?>`, `AtualizarServicoUseCase → Task<Servico?>`, `RemoverServicoUseCase → Task<bool>`, `ListarServicosUseCase → Task<ResultadoListaServicos>`.
  - `Oficina.Aplicacao.Catalogo.ResultadoListaServicos(IReadOnlyList<Servico> Itens, int Total, int Pagina, int TamanhoPagina)`.
  - `Oficina.Aplicacao.Catalogo.PaginaServicos(IReadOnlyList<ServicoResponse> Itens, int Total, int Pagina, int TamanhoPagina)` — **mantida no namespace `Oficina.Aplicacao.Catalogo`** (o teste de integração depende disso).

- [ ] **Step 1: Criar `IServicoGateway` (camada de Casos de Uso)**

Create `src/Oficina.Aplicacao/Catalogo/Gateways/IServicoGateway.cs`:
```csharp
using Oficina.Dominio.Catalogo;

namespace Oficina.Aplicacao.Catalogo.Gateways;

public interface IServicoGateway
{
    Task<Servico?> ObterPorIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<Servico>> ListarAsync(string? filtroNome, int pagina, int tamanhoPagina, bool incluirInativos, CancellationToken ct);
    Task<int> ContarAsync(string? filtroNome, bool incluirInativos, CancellationToken ct);
    Task AdicionarAsync(Servico servico, CancellationToken ct);
    Task SalvarAsync(CancellationToken ct);
}
```

- [ ] **Step 2: Refatorar os 5 use cases para depender do gateway e retornar entidades**

Substituir `src/Oficina.Aplicacao/Catalogo/CriarServicoUseCase.cs`:
```csharp
using Oficina.Aplicacao.Catalogo.Dtos;
using Oficina.Aplicacao.Catalogo.Gateways;
using Oficina.Dominio.Catalogo;

namespace Oficina.Aplicacao.Catalogo;

public class CriarServicoUseCase
{
    private readonly IServicoGateway _gateway;
    public CriarServicoUseCase(IServicoGateway gateway) => _gateway = gateway;

    public async Task<Servico> ExecutarAsync(CriarServicoRequest req, CancellationToken ct)
    {
        var servico = Servico.Criar(req.Nome, req.Descricao, req.PrecoBase, req.TempoEstimadoMinutos);
        await _gateway.AdicionarAsync(servico, ct);
        await _gateway.SalvarAsync(ct);
        return servico;
    }
}
```

Substituir `src/Oficina.Aplicacao/Catalogo/ObterServicoPorIdUseCase.cs`:
```csharp
using Oficina.Aplicacao.Catalogo.Gateways;
using Oficina.Dominio.Catalogo;

namespace Oficina.Aplicacao.Catalogo;

public class ObterServicoPorIdUseCase
{
    private readonly IServicoGateway _gateway;
    public ObterServicoPorIdUseCase(IServicoGateway gateway) => _gateway = gateway;

    public Task<Servico?> ExecutarAsync(Guid id, CancellationToken ct) => _gateway.ObterPorIdAsync(id, ct);
}
```

Substituir `src/Oficina.Aplicacao/Catalogo/ListarServicosUseCase.cs`:
```csharp
using Oficina.Aplicacao.Catalogo.Dtos;
using Oficina.Aplicacao.Catalogo.Gateways;
using Oficina.Dominio.Catalogo;

namespace Oficina.Aplicacao.Catalogo;

// Response DTO consumido pelo Presenter e pelo cliente HTTP (mantido neste namespace por compatibilidade dos testes).
public sealed record PaginaServicos(IReadOnlyList<ServicoResponse> Itens, int Total, int Pagina, int TamanhoPagina);

// Resultado do use case em termos de entidades de domínio (o Presenter converte em PaginaServicos).
public sealed record ResultadoListaServicos(IReadOnlyList<Servico> Itens, int Total, int Pagina, int TamanhoPagina);

public class ListarServicosUseCase
{
    private readonly IServicoGateway _gateway;
    public ListarServicosUseCase(IServicoGateway gateway) => _gateway = gateway;

    public async Task<ResultadoListaServicos> ExecutarAsync(string? filtroNome, int pagina, int tamanho, bool incluirInativos, CancellationToken ct)
    {
        var lista = await _gateway.ListarAsync(filtroNome, pagina, tamanho, incluirInativos, ct);
        var total = await _gateway.ContarAsync(filtroNome, incluirInativos, ct);
        return new ResultadoListaServicos(lista, total, pagina, tamanho);
    }
}
```

Substituir `src/Oficina.Aplicacao/Catalogo/AtualizarServicoUseCase.cs`:
```csharp
using Oficina.Aplicacao.Catalogo.Dtos;
using Oficina.Aplicacao.Catalogo.Gateways;
using Oficina.Dominio.Catalogo;

namespace Oficina.Aplicacao.Catalogo;

public class AtualizarServicoUseCase
{
    private readonly IServicoGateway _gateway;
    public AtualizarServicoUseCase(IServicoGateway gateway) => _gateway = gateway;

    public async Task<Servico?> ExecutarAsync(Guid id, AtualizarServicoRequest req, CancellationToken ct)
    {
        var servico = await _gateway.ObterPorIdAsync(id, ct);
        if (servico is null) return null;

        servico.AtualizarDados(req.Nome, req.Descricao, req.PrecoBase, req.TempoEstimadoMinutos);
        await _gateway.SalvarAsync(ct);
        return servico;
    }
}
```

Substituir `src/Oficina.Aplicacao/Catalogo/RemoverServicoUseCase.cs`:
```csharp
using Oficina.Aplicacao.Catalogo.Gateways;

namespace Oficina.Aplicacao.Catalogo;

public class RemoverServicoUseCase
{
    private readonly IServicoGateway _gateway;
    public RemoverServicoUseCase(IServicoGateway gateway) => _gateway = gateway;

    public async Task<bool> ExecutarAsync(Guid id, CancellationToken ct)
    {
        var servico = await _gateway.ObterPorIdAsync(id, ct);
        if (servico is null) return false;

        servico.Inativar();
        await _gateway.SalvarAsync(ct);
        return true;
    }
}
```

- [ ] **Step 3: Deletar o mapeador da Aplicação (vira Presenter)**

Run: `rm src/Oficina.Aplicacao/Catalogo/MapeadorServicoResponse.cs`

- [ ] **Step 4: Criar `IServicoDataSource` e `ServicoGateway` (Adaptadores)**

Create `src/Oficina.Adaptadores/Catalogo/DataSources/IServicoDataSource.cs`:
```csharp
using Oficina.Dominio.Catalogo;

namespace Oficina.Adaptadores.Catalogo.DataSources;

public interface IServicoDataSource
{
    Task<Servico?> ObterPorIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<Servico>> ListarAsync(string? filtroNome, int pagina, int tamanhoPagina, bool incluirInativos, CancellationToken ct);
    Task<int> ContarAsync(string? filtroNome, bool incluirInativos, CancellationToken ct);
    Task AdicionarAsync(Servico servico, CancellationToken ct);
    Task SalvarAsync(CancellationToken ct);
}
```

Create `src/Oficina.Adaptadores/Catalogo/Gateways/ServicoGateway.cs`:
```csharp
using Oficina.Adaptadores.Catalogo.DataSources;
using Oficina.Aplicacao.Catalogo.Gateways;
using Oficina.Dominio.Catalogo;

namespace Oficina.Adaptadores.Catalogo.Gateways;

public class ServicoGateway : IServicoGateway
{
    private readonly IServicoDataSource _dataSource;
    public ServicoGateway(IServicoDataSource dataSource) => _dataSource = dataSource;

    public Task<Servico?> ObterPorIdAsync(Guid id, CancellationToken ct) =>
        _dataSource.ObterPorIdAsync(id, ct);

    public Task<IReadOnlyList<Servico>> ListarAsync(string? filtroNome, int pagina, int tamanhoPagina, bool incluirInativos, CancellationToken ct) =>
        _dataSource.ListarAsync(filtroNome, pagina, tamanhoPagina, incluirInativos, ct);

    public Task<int> ContarAsync(string? filtroNome, bool incluirInativos, CancellationToken ct) =>
        _dataSource.ContarAsync(filtroNome, incluirInativos, ct);

    public Task AdicionarAsync(Servico servico, CancellationToken ct) =>
        _dataSource.AdicionarAsync(servico, ct);

    public Task SalvarAsync(CancellationToken ct) =>
        _dataSource.SalvarAsync(ct);
}
```

- [ ] **Step 5: Criar `ServicoPresenter` (Adaptadores)**

Create `src/Oficina.Adaptadores/Catalogo/Presenters/ServicoPresenter.cs`:
```csharp
using Oficina.Aplicacao.Catalogo;
using Oficina.Aplicacao.Catalogo.Dtos;
using Oficina.Dominio.Catalogo;

namespace Oficina.Adaptadores.Catalogo.Presenters;

public static class ServicoPresenter
{
    public static ServicoResponse Apresentar(Servico servico) => new(
        servico.Id, servico.Nome, servico.Descricao, servico.PrecoBase,
        servico.TempoEstimadoMinutos, servico.Ativo, servico.CriadoEm);

    public static PaginaServicos ApresentarPagina(IReadOnlyList<Servico> itens, int total, int pagina, int tamanhoPagina) =>
        new(itens.Select(Apresentar).ToList(), total, pagina, tamanhoPagina);
}
```

- [ ] **Step 6: Escrever o teste (falho) do Presenter**

Create `tests/Oficina.Adaptadores.Testes/Catalogo/ServicoPresenterTestes.cs`:
```csharp
using FluentAssertions;
using Oficina.Adaptadores.Catalogo.Presenters;
using Oficina.Dominio.Catalogo;

namespace Oficina.Adaptadores.Testes.Catalogo;

public class ServicoPresenterTestes
{
    [Fact]
    public void Apresentar_DeveMapearTodosOsCamposDaEntidade()
    {
        var servico = Servico.Criar("Troca de óleo", "desc", 150m, 30);

        var resp = ServicoPresenter.Apresentar(servico);

        resp.Id.Should().Be(servico.Id);
        resp.Nome.Should().Be("Troca de óleo");
        resp.PrecoBase.Should().Be(150m);
        resp.TempoEstimadoMinutos.Should().Be(30);
        resp.Ativo.Should().BeTrue();
    }

    [Fact]
    public void ApresentarPagina_DeveMapearItensEMetadados()
    {
        var itens = new[] { Servico.Criar("A", "x", 10m, 5), Servico.Criar("B", "y", 20m, 10) };

        var pagina = ServicoPresenter.ApresentarPagina(itens, total: 2, pagina: 1, tamanhoPagina: 20);

        pagina.Total.Should().Be(2);
        pagina.Pagina.Should().Be(1);
        pagina.TamanhoPagina.Should().Be(20);
        pagina.Itens.Should().HaveCount(2);
        pagina.Itens.Select(i => i.Nome).Should().ContainInOrder("A", "B");
    }
}
```

- [ ] **Step 7: Rodar o teste do Presenter (deve passar; Presenter já existe)**

Run: `dotnet test tests/Oficina.Adaptadores.Testes --filter "FullyQualifiedName~ServicoPresenterTestes"`
Expected: PASS.

- [ ] **Step 8: Criar o `ServicoController` de aplicação (Adaptadores)**

Create `src/Oficina.Adaptadores/Catalogo/Controllers/ServicoController.cs`:
```csharp
using Oficina.Adaptadores.Catalogo.Presenters;
using Oficina.Aplicacao.Catalogo;
using Oficina.Aplicacao.Catalogo.Dtos;

namespace Oficina.Adaptadores.Catalogo.Controllers;

// Controller de aplicação (Adaptadores de Interface): orquestra os casos de uso e formata via Presenter.
public class ServicoController
{
    private readonly CriarServicoUseCase _criar;
    private readonly ObterServicoPorIdUseCase _obter;
    private readonly ListarServicosUseCase _listar;
    private readonly AtualizarServicoUseCase _atualizar;
    private readonly RemoverServicoUseCase _remover;

    public ServicoController(
        CriarServicoUseCase criar,
        ObterServicoPorIdUseCase obter,
        ListarServicosUseCase listar,
        AtualizarServicoUseCase atualizar,
        RemoverServicoUseCase remover)
    {
        _criar = criar;
        _obter = obter;
        _listar = listar;
        _atualizar = atualizar;
        _remover = remover;
    }

    public async Task<ServicoResponse> CriarAsync(CriarServicoRequest req, CancellationToken ct)
    {
        var servico = await _criar.ExecutarAsync(req, ct);
        return ServicoPresenter.Apresentar(servico);
    }

    public async Task<ServicoResponse?> ObterPorIdAsync(Guid id, CancellationToken ct)
    {
        var servico = await _obter.ExecutarAsync(id, ct);
        return servico is null ? null : ServicoPresenter.Apresentar(servico);
    }

    public async Task<PaginaServicos> ListarAsync(string? nome, int pagina, int tamanhoPagina, bool incluirInativos, CancellationToken ct)
    {
        var r = await _listar.ExecutarAsync(nome, pagina, tamanhoPagina, incluirInativos, ct);
        return ServicoPresenter.ApresentarPagina(r.Itens, r.Total, r.Pagina, r.TamanhoPagina);
    }

    public async Task<ServicoResponse?> AtualizarAsync(Guid id, AtualizarServicoRequest req, CancellationToken ct)
    {
        var servico = await _atualizar.ExecutarAsync(id, req, ct);
        return servico is null ? null : ServicoPresenter.Apresentar(servico);
    }

    public Task<bool> RemoverAsync(Guid id, CancellationToken ct) => _remover.ExecutarAsync(id, ct);
}
```

- [ ] **Step 9: Criar o `ServicoDataSource` (Infraestrutura) com o corpo do antigo repositório**

Create `src/Oficina.Infraestrutura/Persistencia/DataSources/ServicoDataSource.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using Oficina.Adaptadores.Catalogo.DataSources;
using Oficina.Dominio.Catalogo;

namespace Oficina.Infraestrutura.Persistencia.DataSources;

public class ServicoDataSource : IServicoDataSource
{
    private readonly OficinaDbContext _db;
    public ServicoDataSource(OficinaDbContext db) => _db = db;

    public Task<Servico?> ObterPorIdAsync(Guid id, CancellationToken ct) =>
        _db.Servicos.FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<IReadOnlyList<Servico>> ListarAsync(
        string? filtroNome, int pagina, int tamanhoPagina, bool incluirInativos, CancellationToken ct)
    {
        if (pagina < 1) pagina = 1;
        if (tamanhoPagina < 1 || tamanhoPagina > 100) tamanhoPagina = 20;

        var query = _db.Servicos.AsQueryable();
        if (!incluirInativos)
            query = query.Where(s => s.Ativo);
        if (!string.IsNullOrWhiteSpace(filtroNome))
            query = query.Where(s => EF.Functions.ILike(s.Nome, $"%{filtroNome}%"));

        return await query
            .OrderBy(s => s.Nome)
            .Skip((pagina - 1) * tamanhoPagina)
            .Take(tamanhoPagina)
            .ToListAsync(ct);
    }

    public Task<int> ContarAsync(string? filtroNome, bool incluirInativos, CancellationToken ct)
    {
        var query = _db.Servicos.AsQueryable();
        if (!incluirInativos)
            query = query.Where(s => s.Ativo);
        if (!string.IsNullOrWhiteSpace(filtroNome))
            query = query.Where(s => EF.Functions.ILike(s.Nome, $"%{filtroNome}%"));
        return query.CountAsync(ct);
    }

    public async Task AdicionarAsync(Servico servico, CancellationToken ct) =>
        await _db.Servicos.AddAsync(servico, ct);

    public Task SalvarAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);
}
```

- [ ] **Step 10: Deletar o antigo repositório e a antiga interface de repositório do Catálogo**

Run:
```bash
rm src/Oficina.Infraestrutura/Persistencia/Repositorios/ServicoRepositorio.cs
rm src/Oficina.Dominio/Catalogo/IServicoRepositorio.cs
```

- [ ] **Step 11: Ajustar o wiring de DI (Infra e Adaptadores)**

Em `src/Oficina.Infraestrutura/DependencyInjectionRepositorios.cs`, trocar o binding do Serviço. Remover a linha:
```csharp
services.AddScoped<IServicoRepositorio, ServicoRepositorio>();
```
e adicionar o registro do DataSource (novo `using` + linha). O arquivo fica:
```csharp
using Microsoft.Extensions.DependencyInjection;
using Oficina.Adaptadores.Catalogo.DataSources;
using Oficina.Dominio.Auth;
using Oficina.Dominio.Clientes;
using Oficina.Dominio.Estoque;
using Oficina.Dominio.OrdensServico;
using Oficina.Infraestrutura.Persistencia.DataSources;
using Oficina.Infraestrutura.Persistencia.Repositorios;

namespace Oficina.Infraestrutura;

public static class DependencyInjectionRepositorios
{
    public static IServiceCollection AdicionarRepositorios(this IServiceCollection services)
    {
        services.AddScoped<IUsuarioRepositorio, UsuarioRepositorio>();
        services.AddScoped<IClienteRepositorio, ClienteRepositorio>();
        services.AddScoped<IPecaRepositorio, PecaRepositorio>();
        services.AddScoped<IOrdemDeServicoRepositorio, OrdemDeServicoRepositorio>();

        // DataSources (Clean Architecture — Frameworks & Drivers)
        services.AddScoped<IServicoDataSource, ServicoDataSource>();
        return services;
    }
}
```

Em `src/Oficina.Adaptadores/DependencyInjectionAdaptadores.cs`, registrar o Gateway e o Controller de aplicação:
```csharp
using Microsoft.Extensions.DependencyInjection;
using Oficina.Adaptadores.Catalogo.Controllers;
using Oficina.Adaptadores.Catalogo.Gateways;
using Oficina.Aplicacao.Catalogo.Gateways;

namespace Oficina.Adaptadores;

public static class DependencyInjectionAdaptadores
{
    public static IServiceCollection AdicionarAdaptadores(this IServiceCollection services)
    {
        // Catálogo
        services.AddScoped<IServicoGateway, ServicoGateway>();
        services.AddScoped<ServicoController>();
        return services;
    }
}
```

- [ ] **Step 12: Deixar o controller HTTP fino (delegando ao `ServicoController`)**

Substituir `src/Oficina.Api/Controllers/ServicosController.cs`:
```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oficina.Adaptadores.Catalogo.Controllers;
using Oficina.Aplicacao.Catalogo.Dtos;
using Oficina.Api.Configuracao;

namespace Oficina.Api.Controllers;

[ApiController]
[Route("api/v1/servicos")]
[Authorize(Policy = PoliticasDeAutorizacao.RequerAdminOuAtendente)]
public class ServicosController : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Criar(
        [FromBody] CriarServicoRequest req,
        [FromServices] ServicoController controller,
        CancellationToken ct)
    {
        var resp = await controller.CriarAsync(req, ct);
        return CreatedAtAction(nameof(Obter), new { id = resp.Id }, resp);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Obter(
        Guid id,
        [FromServices] ServicoController controller,
        CancellationToken ct)
    {
        var resp = await controller.ObterPorIdAsync(id, ct);
        return resp is null ? NotFound() : Ok(resp);
    }

    [HttpGet]
    public async Task<IActionResult> Listar(
        [FromServices] ServicoController controller,
        [FromQuery] string? nome,
        [FromQuery] int pagina = 1,
        [FromQuery] int tamanhoPagina = 20,
        [FromQuery] bool incluirInativos = false,
        CancellationToken ct = default)
    {
        var resp = await controller.ListarAsync(nome, pagina, tamanhoPagina, incluirInativos, ct);
        return Ok(resp);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Atualizar(
        Guid id,
        [FromBody] AtualizarServicoRequest req,
        [FromServices] ServicoController controller,
        CancellationToken ct)
    {
        var resp = await controller.AtualizarAsync(id, req, ct);
        return resp is null ? NotFound() : Ok(resp);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Remover(
        Guid id,
        [FromServices] ServicoController controller,
        CancellationToken ct)
    {
        var ok = await controller.RemoverAsync(id, ct);
        return ok ? NoContent() : NotFound();
    }
}
```

- [ ] **Step 13: Atualizar os testes de use case da Aplicação (mock do Gateway; assert na entidade)**

Substituir `tests/Oficina.Aplicacao.Testes/Catalogo/CriarServicoUseCaseTestes.cs`:
```csharp
using FluentAssertions;
using Moq;
using Oficina.Aplicacao.Catalogo;
using Oficina.Aplicacao.Catalogo.Dtos;
using Oficina.Aplicacao.Catalogo.Gateways;
using Oficina.Dominio.Catalogo;
using Xunit;

namespace Oficina.Aplicacao.Testes.Catalogo;

public class CriarServicoUseCaseTestes
{
    private readonly Mock<IServicoGateway> _gateway = new();

    [Fact]
    public async Task Executar_ComDadosValidos_DevePersistirERetornarEntidade()
    {
        var req = new CriarServicoRequest("Troca de óleo", "desc", 150m, 30);

        var servico = await new CriarServicoUseCase(_gateway.Object).ExecutarAsync(req, default);

        servico.Nome.Should().Be("Troca de óleo");
        servico.PrecoBase.Should().Be(150m);
        servico.Ativo.Should().BeTrue();
        _gateway.Verify(g => g.AdicionarAsync(It.IsAny<Servico>(), It.IsAny<CancellationToken>()), Times.Once);
        _gateway.Verify(g => g.SalvarAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Executar_ComPrecoZero_DevePropagarServicoInvalidoException()
    {
        var req = new CriarServicoRequest("X", "desc", 0m, 30);

        var act = async () => await new CriarServicoUseCase(_gateway.Object).ExecutarAsync(req, default);

        await act.Should().ThrowAsync<ServicoInvalidoException>();
    }
}
```

Abrir `tests/Oficina.Aplicacao.Testes/Catalogo/AtualizarServicoUseCaseTestes.cs` e aplicar a **mesma transformação mecânica**: (1) trocar `Mock<IServicoRepositorio>` por `Mock<IServicoGateway>` e o `using Oficina.Dominio.Catalogo;` do repositório por `using Oficina.Aplicacao.Catalogo.Gateways;`; (2) onde os `Setup`/`Verify` referenciam `IServicoRepositorio`, trocar para `IServicoGateway` (mesma assinatura de métodos); (3) como `AtualizarServicoUseCase.ExecutarAsync` agora retorna `Servico?`, ajustar os asserts que liam `ServicoResponse` para ler as propriedades da entidade `Servico` (ex.: `resultado!.PrecoBase.Should().Be(...)`, `resultado.Nome.Should().Be(...)`). Não mudar os cenários testados.

- [ ] **Step 14: Escrever o teste (falho) do `ServicoGateway` (delegação para o DataSource)**

Create `tests/Oficina.Adaptadores.Testes/Catalogo/ServicoGatewayTestes.cs`:
```csharp
using FluentAssertions;
using Moq;
using Oficina.Adaptadores.Catalogo.DataSources;
using Oficina.Adaptadores.Catalogo.Gateways;
using Oficina.Dominio.Catalogo;

namespace Oficina.Adaptadores.Testes.Catalogo;

public class ServicoGatewayTestes
{
    [Fact]
    public async Task ObterPorIdAsync_DeveDelegarParaODataSource()
    {
        var ds = new Mock<IServicoDataSource>();
        var esperado = Servico.Criar("A", "x", 10m, 5);
        ds.Setup(d => d.ObterPorIdAsync(esperado.Id, It.IsAny<CancellationToken>())).ReturnsAsync(esperado);

        var gateway = new ServicoGateway(ds.Object);
        var obtido = await gateway.ObterPorIdAsync(esperado.Id, default);

        obtido.Should().BeSameAs(esperado);
        ds.Verify(d => d.ObterPorIdAsync(esperado.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AdicionarESalvar_DevemDelegarParaODataSource()
    {
        var ds = new Mock<IServicoDataSource>();
        var gateway = new ServicoGateway(ds.Object);
        var servico = Servico.Criar("A", "x", 10m, 5);

        await gateway.AdicionarAsync(servico, default);
        await gateway.SalvarAsync(default);

        ds.Verify(d => d.AdicionarAsync(servico, It.IsAny<CancellationToken>()), Times.Once);
        ds.Verify(d => d.SalvarAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
```

- [ ] **Step 15: Escrever o teste (falho) do `ServicoController` (orquestração + Presenter)**

Create `tests/Oficina.Adaptadores.Testes/Catalogo/ServicoControllerTestes.cs`:
```csharp
using FluentAssertions;
using Moq;
using Oficina.Adaptadores.Catalogo.Controllers;
using Oficina.Aplicacao.Catalogo;
using Oficina.Aplicacao.Catalogo.Dtos;
using Oficina.Aplicacao.Catalogo.Gateways;
using Oficina.Dominio.Catalogo;

namespace Oficina.Adaptadores.Testes.Catalogo;

public class ServicoControllerTestes
{
    private static ServicoController CriarController(Mock<IServicoGateway> gateway) =>
        new(
            new CriarServicoUseCase(gateway.Object),
            new ObterServicoPorIdUseCase(gateway.Object),
            new ListarServicosUseCase(gateway.Object),
            new AtualizarServicoUseCase(gateway.Object),
            new RemoverServicoUseCase(gateway.Object));

    [Fact]
    public async Task CriarAsync_DevePersistirERetornarServicoResponseFormatado()
    {
        var gateway = new Mock<IServicoGateway>();
        var controller = CriarController(gateway);

        var resp = await controller.CriarAsync(new CriarServicoRequest("Troca de óleo", "d", 150m, 30), default);

        resp.Should().BeOfType<ServicoResponse>();
        resp.Nome.Should().Be("Troca de óleo");
        resp.PrecoBase.Should().Be(150m);
        gateway.Verify(g => g.AdicionarAsync(It.IsAny<Servico>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ObterPorIdAsync_QuandoNaoExiste_DeveRetornarNull()
    {
        var gateway = new Mock<IServicoGateway>();
        gateway.Setup(g => g.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Servico?)null);
        var controller = CriarController(gateway);

        var resp = await controller.ObterPorIdAsync(Guid.NewGuid(), default);

        resp.Should().BeNull();
    }

    [Fact]
    public async Task ListarAsync_DeveMontarPaginaServicos()
    {
        var gateway = new Mock<IServicoGateway>();
        var itens = new[] { Servico.Criar("A", "x", 10m, 5) };
        gateway.Setup(g => g.ListarAsync(null, 1, 20, false, It.IsAny<CancellationToken>())).ReturnsAsync(itens);
        gateway.Setup(g => g.ContarAsync(null, false, It.IsAny<CancellationToken>())).ReturnsAsync(1);
        var controller = CriarController(gateway);

        var pagina = await controller.ListarAsync(null, 1, 20, false, default);

        pagina.Total.Should().Be(1);
        pagina.Itens.Should().ContainSingle(i => i.Nome == "A");
    }
}
```

- [ ] **Step 16: Compilar a solution**

Run: `dotnet build Oficina.sln`
Expected: **Build succeeded**, 0 erros (todas as referências resolvidas; deleções sem referências pendentes).

- [ ] **Step 17: Rodar os testes unitários (Domínio + Aplicação + Adaptadores)**

Run: `dotnet test tests/Oficina.Dominio.Testes tests/Oficina.Aplicacao.Testes tests/Oficina.Adaptadores.Testes`
Expected: PASS em todos (incluindo os novos testes de Presenter/Gateway/Controller e os testes de use case adaptados).

- [ ] **Step 18: Rodar os testes de integração do Catálogo (contrato HTTP intacto — precisa de Docker)**

Run: `dotnet test tests/Oficina.Integracao.Testes --filter "FullyQualifiedName~ServicosEndpointTestes"`
Expected: PASS. (Valida que o refactor não mudou o comportamento externo: criar/obter/atualizar/remover/listar respondem com os mesmos status e JSON.)

- [ ] **Step 19: Rodar a suíte completa + cobertura**

Run: `dotnet test Oficina.sln --collect:"XPlat Code Coverage" --settings coverlet.runsettings`
Expected: todos os testes PASS; cobertura de linha ≥ 80%.

- [ ] **Step 20: Commit**

```bash
git add -A
git commit -m "$(cat <<'EOF'
refactor: Catalogo para Clean Architecture (Gateway/DataSource/Presenter/Controller)

Fatia de referencia do refactor: casos de uso do Catalogo passam a consumir
IServicoGateway e a retornar entidades; ServicoController (aplicacao) orquestra
e formata via ServicoPresenter; ServicoGateway delega para IServicoDataSource,
implementado por ServicoDataSource (EF Core). Controller HTTP fica fino.
Remove IServicoRepositorio, ServicoRepositorio e MapeadorServicoResponse.
Contrato HTTP inalterado (testes de integracao verdes).

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Próximos planos (fora deste arquivo)

Este plano entrega o **scaffold + o padrão de referência (Catálogo)**. Os próximos planos, escritos just-in-time (cada um sobre o código real já refatorado), replicam/estendem o padrão:

- **Plano 02** — Refatorar **Clientes** (Cliente+Veiculo; find-or-create já preparado para a abertura de OS).
- **Plano 03** — Refatorar **Estoque** (Peca+Movimentacao; preservar `EmTransacaoSerializadaAsync` no DataSource).
- **Plano 04** — Refatorar **Ordens de Serviço** (núcleo; máquina de estados; baixa de estoque transacional) e **Auth**; afinar todos os controllers HTTP; remover Domain Events mortos.
- **Plano 05** — Mudanças de API + notificação (abertura consolidada, webhook de aprovação, listagem ordenada, notificação mock).
- **Plano 06** — Docker + docker-compose revisados.
- **Plano 07** — Kubernetes (`/k8s`).
- **Plano 08** — Terraform (`/infra`, AWS).
- **Plano 09** — CI/CD (GitHub Actions + OIDC).
- **Plano 10** — Observabilidade mínima (`/metrics` OpenTelemetry).
- **Plano 11** — Docs e entrega (README + diagramas, ADRs, collection, roteiro de vídeo, PDF, CLAUDE.md).

*(Numeração de planos além do refactor pode ser consolidada; a spec §15 lista 8 blocos lógicos — aqui o refactor foi fatiado por contexto para manter cada plano com entregável testável.)*
