# Fase 2 — Plano 02: Refactor Clean Architecture — contexto Clientes

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Refatorar o contexto **Clientes** (Cliente + Veiculo aninhado) ponta-a-ponta para a Clean Architecture do curso, replicando **exatamente** o padrão de referência já aprovado no Catálogo (Plano 01): Entidades → Casos de Uso (que passam a consumir `IClienteGateway` e a retornar entidades de domínio) → Adaptadores de Interface (`ClienteController` + `ClienteGateway` + `ClientePresenter`) → Frameworks & Drivers (`ClienteDataSource` EF Core em Infraestrutura + `ClientesController` HTTP fino). O comportamento externo (contrato HTTP) **não muda**.

**Architecture:** Os 10 casos de uso de Clientes passam a depender de `IClienteGateway` (definida na camada de Casos de Uso, em `Oficina.Aplicacao`) e a **retornar entidades de domínio** (`Cliente`/`Cliente?`/`bool`/`Veiculo`/`Veiculo?`/`IReadOnlyList<Veiculo>?` e um `ResultadoListaClientes` para a listagem). Um `ClienteController` (Adaptadores) orquestra os casos de uso e usa `ClientePresenter` para formatar a saída nos mesmos DTOs de hoje (`ClienteResponse`, `VeiculoResponse`, `PaginaClientes`). `ClienteGateway` implementa `IClienteGateway` delegando 1:1 para `IClienteDataSource`; `ClienteDataSource` (EF Core, em Infraestrutura) implementa `IClienteDataSource` com o corpo do antigo `ClienteRepositorio` (preserva `.Include(Veiculos)`, o workaround `MarcarVeiculoComoNovo`, a paginação com clamp, o filtro por documento extraindo `.Valor` para variável local, e `Remover`). O controller HTTP `ClientesController` fica fino e só delega para o `ClienteController` de aplicação, preservando rotas, verbos e status codes. Como `IClienteRepositorio` é deletada, os casos de uso de **OrdensServico** e **Consulta** que validavam o cliente migram para `IClienteGateway` (mesmo assembly `Oficina.Aplicacao`).

**Tech Stack:** C# 12 / .NET 8, ASP.NET Core, EF Core 8 + Npgsql, xUnit 2.5.3 + FluentAssertions 6.12.1 + Moq 4.20.72, Testcontainers.PostgreSql (integração). O projeto `Oficina.Adaptadores` e `Oficina.Adaptadores.Testes` **já existem** (Plano 01) — não há task de scaffold.

## Global Constraints

- **Idioma pt-BR** em código, identificadores, comentários e mensagens de commit (convenção do projeto).
- **.NET 8** (`net8.0`), `Nullable=enable`, `ImplicitUsings=enable` em todos os projetos.
- **Regra de dependência** (curso): `Oficina.Api → Oficina.Adaptadores → Oficina.Aplicacao → Oficina.Dominio`; `Oficina.Infraestrutura → Oficina.Adaptadores` (implementa `IClienteDataSource`) + `→ Oficina.Aplicacao` + `→ Oficina.Dominio`; `Oficina.Api → Oficina.Infraestrutura` só para wiring de DI. **`Oficina.Dominio` continua sem dependências externas.** `Oficina.Aplicacao` **não** referencia `Oficina.Adaptadores` — por isso `IClienteGateway` mora em `Oficina.Aplicacao`.
- **DI idiomático**: classes stateless registradas no container (Scoped), sem `new`/`static` manual de dependências (exceto nos testes unitários).
- **Contrato HTTP inalterado**: `ClienteResponse`, `VeiculoResponse`, `PaginaClientes`, rotas `/api/v1/clientes` (+ aninhadas `/{id}/veiculos[/{placa}]`), verbos, status codes (201/200/204/404/409/422), busca por `documento` na query e `[Authorize(Policy = RequerAdminOuAtendente)]`. As exceções de domínio (`DocumentoJaCadastradoException`→409, `PlacaJaCadastradaException`→409, `VeiculoNaoEncontradoException`→404, `DocumentoInvalido`/`PlacaInvalida`/`EmailInvalido`→422 via FluentValidation/middleware) continuam sendo lançadas pelos casos de uso e traduzidas por `MiddlewareDeExcecoes` — **não** tratar exceções no `ClienteController` nem no HTTP controller.
- **Docker indisponível no ambiente local** → **NÃO** rodar os testes de integração (`Oficina.Integracao.Testes`); eles rodam no CI. Gate local = `dotnet build Oficina.sln` (0 erros) + os 3 projetos de teste unitários (Domínio, Aplicação, Adaptadores) verdes.
- **Toolchain .NET 10** no ambiente: rodar **um projeto de teste por chamada** de `dotnet test` (passar múltiplos projetos falha com `MSB1008`).
- **Bash tool = Git Bash** (POSIX sh): usar `rm` e heredoc `<<'EOF'` — **não** usar sintaxe PowerShell.
- **Cobertura de linha ≥ 80%** no CI (gate). DTO/Request/Response já são excluídos por `coverlet.runsettings`.
- **Branch:** `fase-2`. Commits em pt-BR terminando com:
  `Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>`

## Estrutura de arquivos (o que este plano cria/modifica/deleta)

```
src/Oficina.Aplicacao/
  Clientes/Gateways/IClienteGateway.cs                       ★NOVO (abstração consumida pelos use cases)
  Clientes/CriarClienteUseCase.cs                            MODIFICADO (gateway; retorna Cliente)
  Clientes/AtualizarClienteUseCase.cs                        MODIFICADO (gateway; retorna Cliente?)
  Clientes/RemoverClienteUseCase.cs                          MODIFICADO (gateway; retorna bool)
  Clientes/ObterClientePorIdUseCase.cs                       MODIFICADO (gateway; retorna Cliente?)
  Clientes/BuscarClientePorDocumentoUseCase.cs               MODIFICADO (gateway; retorna Cliente?)
  Clientes/ListarClientesUseCase.cs                          MODIFICADO (gateway; retorna ResultadoListaClientes; mantém PaginaClientes)
  Clientes/AdicionarVeiculoUseCase.cs                        MODIFICADO (gateway; retorna Veiculo?)
  Clientes/AtualizarVeiculoUseCase.cs                        MODIFICADO (gateway; retorna Veiculo?)
  Clientes/RemoverVeiculoUseCase.cs                          MODIFICADO (gateway; retorna bool)
  Clientes/ListarVeiculosUseCase.cs                          MODIFICADO (gateway; retorna IReadOnlyList<Veiculo>?)
  Clientes/MapeadorClienteResponse.cs                        DELETADO (vira ClientePresenter)
  OrdensServico/CriarOrdemUseCase.cs                         MODIFICADO (IClienteRepositorio → IClienteGateway)
  Consulta/ConsultarOrdemPorNumeroUseCase.cs                 MODIFICADO (IClienteRepositorio → IClienteGateway)
  Consulta/AprovarOrcamentoPorClienteUseCase.cs              MODIFICADO (IClienteRepositorio → IClienteGateway)
  Consulta/RejeitarOrcamentoPorClienteUseCase.cs             MODIFICADO (IClienteRepositorio → IClienteGateway)

src/Oficina.Adaptadores/
  Clientes/DataSources/IClienteDataSource.cs                 ★NOVO (interface consumida pelo Gateway)
  Clientes/Gateways/ClienteGateway.cs                        ★NOVO (IClienteGateway → delega p/ IClienteDataSource)
  Clientes/Presenters/ClientePresenter.cs                    ★NOVO (Cliente/Veiculo → Response / PaginaClientes)
  Clientes/Controllers/ClienteController.cs                  ★NOVO (orquestra os 10 use cases + Presenter)
  DependencyInjectionAdaptadores.cs                          MODIFICADO (+ IClienteGateway e ClienteController)

src/Oficina.Dominio/
  Clientes/IClienteRepositorio.cs                            DELETADO (vira IClienteGateway)

src/Oficina.Infraestrutura/
  Persistencia/DataSources/ClienteDataSource.cs              ★NOVO (EF Core; corpo do antigo repositório)
  Persistencia/Repositorios/ClienteRepositorio.cs            DELETADO
  DependencyInjectionRepositorios.cs                         MODIFICADO (troca binding Cliente)

src/Oficina.Api/
  Controllers/ClientesController.cs                          MODIFICADO (fino; delega ao ClienteController)

tests/Oficina.Adaptadores.Testes/
  Clientes/ClientePresenterTestes.cs                         ★NOVO
  Clientes/ClienteGatewayTestes.cs                           ★NOVO
  Clientes/ClienteControllerTestes.cs                        ★NOVO

tests/Oficina.Aplicacao.Testes/
  Clientes/CriarClienteUseCaseTestes.cs                      MODIFICADO (mock IClienteGateway; assert entidade)
  Clientes/BuscarClientePorDocumentoUseCaseTestes.cs         MODIFICADO (mock IClienteGateway; assert entidade)
  Clientes/AdicionarVeiculoUseCaseTestes.cs                  MODIFICADO (mock IClienteGateway; assert entidade)
  Clientes/RemoverVeiculoUseCaseTestes.cs                    MODIFICADO (mock IClienteGateway)
  OrdensServico/CriarOrdemUseCaseTestes.cs                   MODIFICADO (Mock<IClienteRepositorio> → Mock<IClienteGateway>)
  Consulta/ConsultarOrdemPorNumeroUseCaseTestes.cs           MODIFICADO (idem)
  Consulta/AprovarOrcamentoPorClienteUseCaseTestes.cs        MODIFICADO (idem)
```

**Resumo:** 9 arquivos criados, 24 modificados, 3 deletados.

---

## Task 1: Refatorar o contexto Clientes para Clean Architecture

Refatora Clientes ponta-a-ponta. Muitos passos pequenos; o build só fica verde ao final (a deleção de `IClienteRepositorio` quebra tudo até que casos de uso, cross-context, DataSource e DI estejam prontos). Ao final: **build verde + os 3 projetos de teste unitários verdes**, com o contrato HTTP intacto (validado no CI pelos testes de integração).

**Files:**
- Create: `src/Oficina.Aplicacao/Clientes/Gateways/IClienteGateway.cs`
- Create: `src/Oficina.Adaptadores/Clientes/DataSources/IClienteDataSource.cs`
- Create: `src/Oficina.Adaptadores/Clientes/Gateways/ClienteGateway.cs`
- Create: `src/Oficina.Adaptadores/Clientes/Presenters/ClientePresenter.cs`
- Create: `src/Oficina.Adaptadores/Clientes/Controllers/ClienteController.cs`
- Create: `src/Oficina.Infraestrutura/Persistencia/DataSources/ClienteDataSource.cs`
- Create: `tests/Oficina.Adaptadores.Testes/Clientes/ClientePresenterTestes.cs`
- Create: `tests/Oficina.Adaptadores.Testes/Clientes/ClienteGatewayTestes.cs`
- Create: `tests/Oficina.Adaptadores.Testes/Clientes/ClienteControllerTestes.cs`
- Modify: os 10 use cases de `src/Oficina.Aplicacao/Clientes/`
- Modify: `src/Oficina.Aplicacao/OrdensServico/CriarOrdemUseCase.cs`, `src/Oficina.Aplicacao/Consulta/{ConsultarOrdemPorNumero,AprovarOrcamentoPorCliente,RejeitarOrcamentoPorCliente}UseCase.cs`
- Modify: `src/Oficina.Api/Controllers/ClientesController.cs`
- Modify: `src/Oficina.Infraestrutura/DependencyInjectionRepositorios.cs`
- Modify: `src/Oficina.Adaptadores/DependencyInjectionAdaptadores.cs`
- Modify: `tests/Oficina.Aplicacao.Testes/Clientes/{CriarCliente,BuscarClientePorDocumento,AdicionarVeiculo,RemoverVeiculo}UseCaseTestes.cs`
- Modify: `tests/Oficina.Aplicacao.Testes/OrdensServico/CriarOrdemUseCaseTestes.cs`, `tests/Oficina.Aplicacao.Testes/Consulta/{ConsultarOrdemPorNumero,AprovarOrcamentoPorCliente}UseCaseTestes.cs`
- Delete: `src/Oficina.Dominio/Clientes/IClienteRepositorio.cs`, `src/Oficina.Infraestrutura/Persistencia/Repositorios/ClienteRepositorio.cs`, `src/Oficina.Aplicacao/Clientes/MapeadorClienteResponse.cs`

**Interfaces (padrão idêntico ao Catálogo):**
- `Oficina.Aplicacao.Clientes.Gateways.IClienteGateway` — mesma forma do antigo `IClienteRepositorio` (inclui `Remover(Cliente)` e `MarcarVeiculoComoNovo(Veiculo)`).
- `Oficina.Adaptadores.Clientes.DataSources.IClienteDataSource` — mesma forma.
- `Oficina.Adaptadores.Clientes.Gateways.ClienteGateway : IClienteGateway`.
- `Oficina.Adaptadores.Clientes.Presenters.ClientePresenter` (estático): `Apresentar(Cliente) : ClienteResponse`, `ApresentarVeiculo(Veiculo) : VeiculoResponse`, `ApresentarPagina(IReadOnlyList<Cliente>, int, int, int) : PaginaClientes`.
- `Oficina.Adaptadores.Clientes.Controllers.ClienteController` — `CriarAsync`, `ObterPorIdAsync`, `BuscarPorDocumentoAsync`, `ListarAsync`, `AtualizarAsync`, `RemoverAsync`, `AdicionarVeiculoAsync`, `AtualizarVeiculoAsync`, `RemoverVeiculoAsync`, `ListarVeiculosAsync`.
- Use cases retornam entidade: `CriarClienteUseCase → Task<Cliente>`, `ObterClientePorIdUseCase → Task<Cliente?>`, `BuscarClientePorDocumentoUseCase → Task<Cliente?>`, `AtualizarClienteUseCase → Task<Cliente?>`, `RemoverClienteUseCase → Task<bool>`, `ListarClientesUseCase → Task<ResultadoListaClientes>`, `AdicionarVeiculoUseCase → Task<Veiculo?>`, `AtualizarVeiculoUseCase → Task<Veiculo?>`, `RemoverVeiculoUseCase → Task<bool>`, `ListarVeiculosUseCase → Task<IReadOnlyList<Veiculo>?>`.
- `Oficina.Aplicacao.Clientes.ResultadoListaClientes(IReadOnlyList<Cliente> Itens, int Total, int Pagina, int TamanhoPagina)`.
- `Oficina.Aplicacao.Clientes.PaginaClientes(IReadOnlyList<ClienteResponse> Itens, int Total, int Pagina, int TamanhoPagina)` — **mantida no namespace `Oficina.Aplicacao.Clientes`** (contrato inalterado).

---

- [ ] **Step 1: Criar `IClienteGateway` (camada de Casos de Uso)**

Create `src/Oficina.Aplicacao/Clientes/Gateways/IClienteGateway.cs`:
```csharp
using Oficina.Dominio.Clientes;

namespace Oficina.Aplicacao.Clientes.Gateways;

public interface IClienteGateway
{
    Task<Cliente?> ObterPorIdAsync(Guid id, CancellationToken ct);
    Task<Cliente?> ObterPorDocumentoAsync(Documento documento, CancellationToken ct);
    Task<bool> ExisteDocumentoAsync(Documento documento, CancellationToken ct);
    Task<IReadOnlyList<Cliente>> ListarAsync(int pagina, int tamanhoPagina, CancellationToken ct);
    Task<int> ContarAsync(CancellationToken ct);
    Task AdicionarAsync(Cliente cliente, CancellationToken ct);
    Task SalvarAsync(CancellationToken ct);
    void Remover(Cliente cliente);

    // EF Core nao detecta entidades adicionadas via navigation collection
    // como Added quando o Id ja vem preenchido — gera UPDATE em vez de
    // INSERT, falhando com DbUpdateConcurrencyException. Esta sobrecarga
    // marca a entidade como Added explicitamente no change tracker.
    void MarcarVeiculoComoNovo(Veiculo veiculo);
}
```

- [ ] **Step 2: Refatorar os 10 casos de uso de Clientes (depender do gateway; retornar entidades)**

Substituir `src/Oficina.Aplicacao/Clientes/CriarClienteUseCase.cs`:
```csharp
using Oficina.Aplicacao.Clientes.Dtos;
using Oficina.Aplicacao.Clientes.Gateways;
using Oficina.Dominio.Clientes;

namespace Oficina.Aplicacao.Clientes;

public class CriarClienteUseCase
{
    private readonly IClienteGateway _gateway;

    public CriarClienteUseCase(IClienteGateway gateway) => _gateway = gateway;

    public async Task<Cliente> ExecutarAsync(CriarClienteRequest req, CancellationToken ct)
    {
        var documento = Documento.Criar(req.Documento);

        if (await _gateway.ExisteDocumentoAsync(documento, ct))
            throw new DocumentoJaCadastradoException(documento.Valor);

        var cliente = Cliente.Criar(
            req.Nome,
            documento,
            Email.Criar(req.Email),
            Telefone.Criar(req.Telefone));

        await _gateway.AdicionarAsync(cliente, ct);
        await _gateway.SalvarAsync(ct);

        return cliente;
    }
}
```

Substituir `src/Oficina.Aplicacao/Clientes/AtualizarClienteUseCase.cs`:
```csharp
using Oficina.Aplicacao.Clientes.Dtos;
using Oficina.Aplicacao.Clientes.Gateways;
using Oficina.Dominio.Clientes;

namespace Oficina.Aplicacao.Clientes;

public class AtualizarClienteUseCase
{
    private readonly IClienteGateway _gateway;
    public AtualizarClienteUseCase(IClienteGateway gateway) => _gateway = gateway;

    public async Task<Cliente?> ExecutarAsync(Guid id, AtualizarClienteRequest req, CancellationToken ct)
    {
        var cliente = await _gateway.ObterPorIdAsync(id, ct);
        if (cliente is null) return null;

        cliente.AtualizarContato(
            req.Nome,
            Email.Criar(req.Email),
            Telefone.Criar(req.Telefone));

        await _gateway.SalvarAsync(ct);
        return cliente;
    }
}
```

Substituir `src/Oficina.Aplicacao/Clientes/RemoverClienteUseCase.cs`:
```csharp
using Oficina.Aplicacao.Clientes.Gateways;

namespace Oficina.Aplicacao.Clientes;

public class RemoverClienteUseCase
{
    private readonly IClienteGateway _gateway;
    public RemoverClienteUseCase(IClienteGateway gateway) => _gateway = gateway;

    public async Task<bool> ExecutarAsync(Guid id, CancellationToken ct)
    {
        var cliente = await _gateway.ObterPorIdAsync(id, ct);
        if (cliente is null) return false;

        cliente.Inativar(); // soft delete
        await _gateway.SalvarAsync(ct);
        return true;
    }
}
```

Substituir `src/Oficina.Aplicacao/Clientes/ObterClientePorIdUseCase.cs`:
```csharp
using Oficina.Aplicacao.Clientes.Gateways;
using Oficina.Dominio.Clientes;

namespace Oficina.Aplicacao.Clientes;

public class ObterClientePorIdUseCase
{
    private readonly IClienteGateway _gateway;
    public ObterClientePorIdUseCase(IClienteGateway gateway) => _gateway = gateway;

    public Task<Cliente?> ExecutarAsync(Guid id, CancellationToken ct) => _gateway.ObterPorIdAsync(id, ct);
}
```

Substituir `src/Oficina.Aplicacao/Clientes/BuscarClientePorDocumentoUseCase.cs`:
```csharp
using Oficina.Aplicacao.Clientes.Gateways;
using Oficina.Dominio.Clientes;

namespace Oficina.Aplicacao.Clientes;

public class BuscarClientePorDocumentoUseCase
{
    private readonly IClienteGateway _gateway;
    public BuscarClientePorDocumentoUseCase(IClienteGateway gateway) => _gateway = gateway;

    public async Task<Cliente?> ExecutarAsync(string documentoBruto, CancellationToken ct)
    {
        var doc = Documento.Criar(documentoBruto);
        return await _gateway.ObterPorDocumentoAsync(doc, ct);
    }
}
```

Substituir `src/Oficina.Aplicacao/Clientes/ListarClientesUseCase.cs`:
```csharp
using Oficina.Aplicacao.Clientes.Dtos;
using Oficina.Aplicacao.Clientes.Gateways;
using Oficina.Dominio.Clientes;

namespace Oficina.Aplicacao.Clientes;

// Response DTO consumido pelo Presenter e pelo cliente HTTP (mantido neste namespace por compatibilidade).
public sealed record PaginaClientes(IReadOnlyList<ClienteResponse> Itens, int Total, int Pagina, int TamanhoPagina);

// Resultado do use case em termos de entidades de domínio (o Presenter converte em PaginaClientes).
public sealed record ResultadoListaClientes(IReadOnlyList<Cliente> Itens, int Total, int Pagina, int TamanhoPagina);

public class ListarClientesUseCase
{
    private readonly IClienteGateway _gateway;
    public ListarClientesUseCase(IClienteGateway gateway) => _gateway = gateway;

    public async Task<ResultadoListaClientes> ExecutarAsync(int pagina, int tamanho, CancellationToken ct)
    {
        var clientes = await _gateway.ListarAsync(pagina, tamanho, ct);
        var total = await _gateway.ContarAsync(ct);
        return new ResultadoListaClientes(clientes, total, pagina, tamanho);
    }
}
```

Substituir `src/Oficina.Aplicacao/Clientes/AdicionarVeiculoUseCase.cs`:
```csharp
using Oficina.Aplicacao.Clientes.Dtos;
using Oficina.Aplicacao.Clientes.Gateways;
using Oficina.Dominio.Clientes;

namespace Oficina.Aplicacao.Clientes;

public class AdicionarVeiculoUseCase
{
    private readonly IClienteGateway _gateway;
    public AdicionarVeiculoUseCase(IClienteGateway gateway) => _gateway = gateway;

    public async Task<Veiculo?> ExecutarAsync(Guid clienteId, AdicionarVeiculoRequest req, CancellationToken ct)
    {
        var cliente = await _gateway.ObterPorIdAsync(clienteId, ct);
        if (cliente is null) return null;

        var veiculo = Veiculo.Criar(Placa.Criar(req.Placa), req.Marca, req.Modelo, req.Ano);
        cliente.AdicionarVeiculo(veiculo);
        _gateway.MarcarVeiculoComoNovo(veiculo);

        await _gateway.SalvarAsync(ct);
        return veiculo;
    }
}
```

Substituir `src/Oficina.Aplicacao/Clientes/AtualizarVeiculoUseCase.cs`:
```csharp
using Oficina.Aplicacao.Clientes.Dtos;
using Oficina.Aplicacao.Clientes.Gateways;
using Oficina.Dominio.Clientes;

namespace Oficina.Aplicacao.Clientes;

public class AtualizarVeiculoUseCase
{
    private readonly IClienteGateway _gateway;
    public AtualizarVeiculoUseCase(IClienteGateway gateway) => _gateway = gateway;

    public async Task<Veiculo?> ExecutarAsync(Guid clienteId, string placaBruta, AtualizarVeiculoRequest req, CancellationToken ct)
    {
        var cliente = await _gateway.ObterPorIdAsync(clienteId, ct);
        if (cliente is null) return null;

        var placa = Placa.Criar(placaBruta);
        cliente.AtualizarVeiculo(placa, req.Marca, req.Modelo, req.Ano);

        await _gateway.SalvarAsync(ct);
        return cliente.Veiculos.First(x => x.Placa.Equals(placa));
    }
}
```

Substituir `src/Oficina.Aplicacao/Clientes/RemoverVeiculoUseCase.cs`:
```csharp
using Oficina.Aplicacao.Clientes.Gateways;
using Oficina.Dominio.Clientes;

namespace Oficina.Aplicacao.Clientes;

public class RemoverVeiculoUseCase
{
    private readonly IClienteGateway _gateway;
    public RemoverVeiculoUseCase(IClienteGateway gateway) => _gateway = gateway;

    public async Task<bool> ExecutarAsync(Guid clienteId, string placaBruta, CancellationToken ct)
    {
        var cliente = await _gateway.ObterPorIdAsync(clienteId, ct);
        if (cliente is null) return false;

        cliente.RemoverVeiculo(Placa.Criar(placaBruta));
        await _gateway.SalvarAsync(ct);
        return true;
    }
}
```

Substituir `src/Oficina.Aplicacao/Clientes/ListarVeiculosUseCase.cs`:
```csharp
using Oficina.Aplicacao.Clientes.Gateways;
using Oficina.Dominio.Clientes;

namespace Oficina.Aplicacao.Clientes;

public class ListarVeiculosUseCase
{
    private readonly IClienteGateway _gateway;
    public ListarVeiculosUseCase(IClienteGateway gateway) => _gateway = gateway;

    public async Task<IReadOnlyList<Veiculo>?> ExecutarAsync(Guid clienteId, CancellationToken ct)
    {
        var cliente = await _gateway.ObterPorIdAsync(clienteId, ct);
        if (cliente is null) return null;
        return cliente.Veiculos.ToList();
    }
}
```

- [ ] **Step 3: Deletar o mapeador da Aplicação (vira Presenter)**

Run:
```bash
cd "C:/Users/thiago/source/repos/Fiap/fiap_15SOAT_fase1"
rm src/Oficina.Aplicacao/Clientes/MapeadorClienteResponse.cs
```

- [ ] **Step 4: Criar `IClienteDataSource` e `ClienteGateway` (Adaptadores)**

Create `src/Oficina.Adaptadores/Clientes/DataSources/IClienteDataSource.cs`:
```csharp
using Oficina.Dominio.Clientes;

namespace Oficina.Adaptadores.Clientes.DataSources;

public interface IClienteDataSource
{
    Task<Cliente?> ObterPorIdAsync(Guid id, CancellationToken ct);
    Task<Cliente?> ObterPorDocumentoAsync(Documento documento, CancellationToken ct);
    Task<bool> ExisteDocumentoAsync(Documento documento, CancellationToken ct);
    Task<IReadOnlyList<Cliente>> ListarAsync(int pagina, int tamanhoPagina, CancellationToken ct);
    Task<int> ContarAsync(CancellationToken ct);
    Task AdicionarAsync(Cliente cliente, CancellationToken ct);
    Task SalvarAsync(CancellationToken ct);
    void Remover(Cliente cliente);
    void MarcarVeiculoComoNovo(Veiculo veiculo);
}
```

Create `src/Oficina.Adaptadores/Clientes/Gateways/ClienteGateway.cs`:
```csharp
using Oficina.Adaptadores.Clientes.DataSources;
using Oficina.Aplicacao.Clientes.Gateways;
using Oficina.Dominio.Clientes;

namespace Oficina.Adaptadores.Clientes.Gateways;

public class ClienteGateway : IClienteGateway
{
    private readonly IClienteDataSource _dataSource;
    public ClienteGateway(IClienteDataSource dataSource) => _dataSource = dataSource;

    public Task<Cliente?> ObterPorIdAsync(Guid id, CancellationToken ct) =>
        _dataSource.ObterPorIdAsync(id, ct);

    public Task<Cliente?> ObterPorDocumentoAsync(Documento documento, CancellationToken ct) =>
        _dataSource.ObterPorDocumentoAsync(documento, ct);

    public Task<bool> ExisteDocumentoAsync(Documento documento, CancellationToken ct) =>
        _dataSource.ExisteDocumentoAsync(documento, ct);

    public Task<IReadOnlyList<Cliente>> ListarAsync(int pagina, int tamanhoPagina, CancellationToken ct) =>
        _dataSource.ListarAsync(pagina, tamanhoPagina, ct);

    public Task<int> ContarAsync(CancellationToken ct) =>
        _dataSource.ContarAsync(ct);

    public Task AdicionarAsync(Cliente cliente, CancellationToken ct) =>
        _dataSource.AdicionarAsync(cliente, ct);

    public Task SalvarAsync(CancellationToken ct) =>
        _dataSource.SalvarAsync(ct);

    public void Remover(Cliente cliente) =>
        _dataSource.Remover(cliente);

    public void MarcarVeiculoComoNovo(Veiculo veiculo) =>
        _dataSource.MarcarVeiculoComoNovo(veiculo);
}
```

- [ ] **Step 5: Criar `ClientePresenter` (Adaptadores)**

Create `src/Oficina.Adaptadores/Clientes/Presenters/ClientePresenter.cs`:
```csharp
using Oficina.Aplicacao.Clientes;
using Oficina.Aplicacao.Clientes.Dtos;
using Oficina.Dominio.Clientes;

namespace Oficina.Adaptadores.Clientes.Presenters;

public static class ClientePresenter
{
    public static ClienteResponse Apresentar(Cliente c) => new(
        c.Id,
        c.Nome,
        c.Documento.Tipo.ToString(),
        c.Documento.Valor,
        c.Documento.Mascarado(),
        c.Email.Valor,
        c.Telefone.Valor,
        c.Ativo,
        c.CriadoEm,
        c.Veiculos.Select(ApresentarVeiculo).ToList());

    public static VeiculoResponse ApresentarVeiculo(Veiculo v) =>
        new(v.Id, v.Placa.Valor, v.Marca, v.Modelo, v.Ano);

    public static PaginaClientes ApresentarPagina(IReadOnlyList<Cliente> itens, int total, int pagina, int tamanhoPagina) =>
        new(itens.Select(Apresentar).ToList(), total, pagina, tamanhoPagina);
}
```

- [ ] **Step 6: Escrever o teste do `ClientePresenter`**

Create `tests/Oficina.Adaptadores.Testes/Clientes/ClientePresenterTestes.cs`:
```csharp
using FluentAssertions;
using Oficina.Adaptadores.Clientes.Presenters;
using Oficina.Dominio.Clientes;

namespace Oficina.Adaptadores.Testes.Clientes;

public class ClientePresenterTestes
{
    private static Cliente CriarClienteComVeiculo()
    {
        var cliente = Cliente.Criar("João", Documento.Criar("39053344705"),
            Email.Criar("joao@x.com"), Telefone.Criar("11987654321"));
        cliente.AdicionarVeiculo(Veiculo.Criar(Placa.Criar("ABC1234"), "Fiat", "Uno", 2020));
        return cliente;
    }

    [Fact]
    public void Apresentar_DeveMapearTodosOsCamposDaEntidade()
    {
        var cliente = CriarClienteComVeiculo();

        var resp = ClientePresenter.Apresentar(cliente);

        resp.Id.Should().Be(cliente.Id);
        resp.Nome.Should().Be("João");
        resp.TipoPessoa.Should().Be("PF");
        resp.Documento.Should().Be("39053344705");
        resp.DocumentoMascarado.Should().Be("390.533.447-05");
        resp.Email.Should().Be("joao@x.com");
        resp.Telefone.Should().Be("11987654321");
        resp.Ativo.Should().BeTrue();
        resp.Veiculos.Should().ContainSingle(v => v.Placa == "ABC1234");
    }

    [Fact]
    public void ApresentarVeiculo_DeveMapearCampos()
    {
        var veiculo = Veiculo.Criar(Placa.Criar("ABC1234"), "Fiat", "Uno", 2020);

        var resp = ClientePresenter.ApresentarVeiculo(veiculo);

        resp.Id.Should().Be(veiculo.Id);
        resp.Placa.Should().Be("ABC1234");
        resp.Marca.Should().Be("Fiat");
        resp.Modelo.Should().Be("Uno");
        resp.Ano.Should().Be(2020);
    }

    [Fact]
    public void ApresentarPagina_DeveMapearItensEMetadados()
    {
        var itens = new[] { CriarClienteComVeiculo() };

        var pagina = ClientePresenter.ApresentarPagina(itens, total: 1, pagina: 1, tamanhoPagina: 20);

        pagina.Total.Should().Be(1);
        pagina.Pagina.Should().Be(1);
        pagina.TamanhoPagina.Should().Be(20);
        pagina.Itens.Should().ContainSingle(c => c.Nome == "João");
    }
}
```

- [ ] **Step 7: Criar o `ClienteController` de aplicação (Adaptadores)**

Create `src/Oficina.Adaptadores/Clientes/Controllers/ClienteController.cs`:
```csharp
using Oficina.Adaptadores.Clientes.Presenters;
using Oficina.Aplicacao.Clientes;
using Oficina.Aplicacao.Clientes.Dtos;

namespace Oficina.Adaptadores.Clientes.Controllers;

// Controller de aplicação (Adaptadores de Interface): orquestra os casos de uso e formata via Presenter.
public class ClienteController
{
    private readonly CriarClienteUseCase _criar;
    private readonly ObterClientePorIdUseCase _obter;
    private readonly BuscarClientePorDocumentoUseCase _buscar;
    private readonly ListarClientesUseCase _listar;
    private readonly AtualizarClienteUseCase _atualizar;
    private readonly RemoverClienteUseCase _remover;
    private readonly AdicionarVeiculoUseCase _adicionarVeiculo;
    private readonly AtualizarVeiculoUseCase _atualizarVeiculo;
    private readonly RemoverVeiculoUseCase _removerVeiculo;
    private readonly ListarVeiculosUseCase _listarVeiculos;

    public ClienteController(
        CriarClienteUseCase criar,
        ObterClientePorIdUseCase obter,
        BuscarClientePorDocumentoUseCase buscar,
        ListarClientesUseCase listar,
        AtualizarClienteUseCase atualizar,
        RemoverClienteUseCase remover,
        AdicionarVeiculoUseCase adicionarVeiculo,
        AtualizarVeiculoUseCase atualizarVeiculo,
        RemoverVeiculoUseCase removerVeiculo,
        ListarVeiculosUseCase listarVeiculos)
    {
        _criar = criar;
        _obter = obter;
        _buscar = buscar;
        _listar = listar;
        _atualizar = atualizar;
        _remover = remover;
        _adicionarVeiculo = adicionarVeiculo;
        _atualizarVeiculo = atualizarVeiculo;
        _removerVeiculo = removerVeiculo;
        _listarVeiculos = listarVeiculos;
    }

    public async Task<ClienteResponse> CriarAsync(CriarClienteRequest req, CancellationToken ct)
    {
        var cliente = await _criar.ExecutarAsync(req, ct);
        return ClientePresenter.Apresentar(cliente);
    }

    public async Task<ClienteResponse?> ObterPorIdAsync(Guid id, CancellationToken ct)
    {
        var cliente = await _obter.ExecutarAsync(id, ct);
        return cliente is null ? null : ClientePresenter.Apresentar(cliente);
    }

    public async Task<ClienteResponse?> BuscarPorDocumentoAsync(string documento, CancellationToken ct)
    {
        var cliente = await _buscar.ExecutarAsync(documento, ct);
        return cliente is null ? null : ClientePresenter.Apresentar(cliente);
    }

    public async Task<PaginaClientes> ListarAsync(int pagina, int tamanhoPagina, CancellationToken ct)
    {
        var r = await _listar.ExecutarAsync(pagina, tamanhoPagina, ct);
        return ClientePresenter.ApresentarPagina(r.Itens, r.Total, r.Pagina, r.TamanhoPagina);
    }

    public async Task<ClienteResponse?> AtualizarAsync(Guid id, AtualizarClienteRequest req, CancellationToken ct)
    {
        var cliente = await _atualizar.ExecutarAsync(id, req, ct);
        return cliente is null ? null : ClientePresenter.Apresentar(cliente);
    }

    public Task<bool> RemoverAsync(Guid id, CancellationToken ct) => _remover.ExecutarAsync(id, ct);

    public async Task<VeiculoResponse?> AdicionarVeiculoAsync(Guid clienteId, AdicionarVeiculoRequest req, CancellationToken ct)
    {
        var veiculo = await _adicionarVeiculo.ExecutarAsync(clienteId, req, ct);
        return veiculo is null ? null : ClientePresenter.ApresentarVeiculo(veiculo);
    }

    public async Task<VeiculoResponse?> AtualizarVeiculoAsync(Guid clienteId, string placa, AtualizarVeiculoRequest req, CancellationToken ct)
    {
        var veiculo = await _atualizarVeiculo.ExecutarAsync(clienteId, placa, req, ct);
        return veiculo is null ? null : ClientePresenter.ApresentarVeiculo(veiculo);
    }

    public Task<bool> RemoverVeiculoAsync(Guid clienteId, string placa, CancellationToken ct) =>
        _removerVeiculo.ExecutarAsync(clienteId, placa, ct);

    public async Task<IReadOnlyList<VeiculoResponse>?> ListarVeiculosAsync(Guid clienteId, CancellationToken ct)
    {
        var veiculos = await _listarVeiculos.ExecutarAsync(clienteId, ct);
        return veiculos?.Select(ClientePresenter.ApresentarVeiculo).ToList();
    }
}
```

- [ ] **Step 8: Criar o `ClienteDataSource` (Infraestrutura) com o corpo do antigo repositório**

Create `src/Oficina.Infraestrutura/Persistencia/DataSources/ClienteDataSource.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using Oficina.Adaptadores.Clientes.DataSources;
using Oficina.Dominio.Clientes;

namespace Oficina.Infraestrutura.Persistencia.DataSources;

public class ClienteDataSource : IClienteDataSource
{
    private readonly OficinaDbContext _db;

    public ClienteDataSource(OficinaDbContext db) => _db = db;

    public Task<Cliente?> ObterPorIdAsync(Guid id, CancellationToken ct) =>
        _db.Clientes.Include(c => c.Veiculos).FirstOrDefaultAsync(c => c.Id == id, ct);

    public Task<Cliente?> ObterPorDocumentoAsync(Documento documento, CancellationToken ct)
    {
        var v = documento.Valor;
        return _db.Clientes.Include(c => c.Veiculos)
            .FirstOrDefaultAsync(c => c.Documento.Valor == v, ct);
    }

    public Task<bool> ExisteDocumentoAsync(Documento documento, CancellationToken ct)
    {
        var v = documento.Valor;
        return _db.Clientes.AnyAsync(c => c.Documento.Valor == v, ct);
    }

    public async Task<IReadOnlyList<Cliente>> ListarAsync(int pagina, int tamanhoPagina, CancellationToken ct)
    {
        if (pagina < 1) pagina = 1;
        if (tamanhoPagina < 1 || tamanhoPagina > 100) tamanhoPagina = 20;

        return await _db.Clientes
            .Include(c => c.Veiculos)
            .Where(c => c.Ativo)
            .OrderBy(c => c.Nome)
            .Skip((pagina - 1) * tamanhoPagina)
            .Take(tamanhoPagina)
            .ToListAsync(ct);
    }

    public Task<int> ContarAsync(CancellationToken ct) =>
        _db.Clientes.CountAsync(c => c.Ativo, ct);

    public async Task AdicionarAsync(Cliente cliente, CancellationToken ct) =>
        await _db.Clientes.AddAsync(cliente, ct);

    public Task SalvarAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);

    public void Remover(Cliente cliente) => _db.Clientes.Remove(cliente);

    public void MarcarVeiculoComoNovo(Veiculo veiculo) =>
        // Set<Veiculo>().Add caminha a entity graph (inclui owned types como Placa);
        // Entry(...).State = Added marca so o root e deixa owneds Detached -> NOT NULL constraint
        _db.Set<Veiculo>().Add(veiculo);
}
```

- [ ] **Step 9: Deletar o antigo repositório e a antiga interface de repositório de Clientes**

Run:
```bash
cd "C:/Users/thiago/source/repos/Fiap/fiap_15SOAT_fase1"
rm src/Oficina.Infraestrutura/Persistencia/Repositorios/ClienteRepositorio.cs
rm src/Oficina.Dominio/Clientes/IClienteRepositorio.cs
```

- [ ] **Step 10: Migrar os casos de uso cross-context (OrdensServico + Consulta) para `IClienteGateway`**

Estes casos de uso usam apenas `_clientes.ObterPorIdAsync(...)`, que existe em `IClienteGateway`. Vivem no assembly `Oficina.Aplicacao`, então podem referenciar `Oficina.Aplicacao.Clientes.Gateways.IClienteGateway`. Em cada arquivo: (a) adicionar `using Oficina.Aplicacao.Clientes.Gateways;` (manter o `using Oficina.Dominio.Clientes;` — ainda usam `Documento`/`Cliente`); (b) trocar o tipo do campo e do parâmetro do construtor de `IClienteRepositorio` para `IClienteGateway`.

Em `src/Oficina.Aplicacao/OrdensServico/CriarOrdemUseCase.cs`:
- Adicionar no topo (antes de `using Oficina.Aplicacao.OrdensServico.Dtos;`): `using Oficina.Aplicacao.Clientes.Gateways;`
- Trocar `private readonly IClienteRepositorio _clientes;` por `private readonly IClienteGateway _clientes;`
- Trocar `public CriarOrdemUseCase(IOrdemDeServicoRepositorio repo, IClienteRepositorio clientes)` por `public CriarOrdemUseCase(IOrdemDeServicoRepositorio repo, IClienteGateway clientes)`

Em `src/Oficina.Aplicacao/Consulta/ConsultarOrdemPorNumeroUseCase.cs`:
- Adicionar no topo (antes de `using Oficina.Aplicacao.Consulta.Dtos;`): `using Oficina.Aplicacao.Clientes.Gateways;`
- Trocar `private readonly IClienteRepositorio _clientes;` por `private readonly IClienteGateway _clientes;`
- Trocar o parâmetro do construtor `IOrdemDeServicoRepositorio ordens, IClienteRepositorio clientes` por `IOrdemDeServicoRepositorio ordens, IClienteGateway clientes`

Em `src/Oficina.Aplicacao/Consulta/AprovarOrcamentoPorClienteUseCase.cs`: aplicar as **mesmas três** trocas (using + campo + parâmetro do construtor).

Em `src/Oficina.Aplicacao/Consulta/RejeitarOrcamentoPorClienteUseCase.cs`: aplicar as **mesmas três** trocas (using + campo + parâmetro do construtor).

- [ ] **Step 11: Ajustar o wiring de DI (Infra e Adaptadores)**

Substituir `src/Oficina.Infraestrutura/DependencyInjectionRepositorios.cs` (remove o binding do `IClienteRepositorio`/`ClienteRepositorio` e o `using Oficina.Dominio.Clientes;` que só servia a ele; adiciona o `IClienteDataSource`):
```csharp
using Microsoft.Extensions.DependencyInjection;
using Oficina.Adaptadores.Catalogo.DataSources;
using Oficina.Adaptadores.Clientes.DataSources;
using Oficina.Dominio.Auth;
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
        services.AddScoped<IPecaRepositorio, PecaRepositorio>();
        services.AddScoped<IOrdemDeServicoRepositorio, OrdemDeServicoRepositorio>();

        // DataSources (Clean Architecture — Frameworks & Drivers)
        services.AddScoped<IServicoDataSource, ServicoDataSource>();
        services.AddScoped<IClienteDataSource, ClienteDataSource>();
        return services;
    }
}
```

Substituir `src/Oficina.Adaptadores/DependencyInjectionAdaptadores.cs` (adiciona o Gateway e o Controller de Clientes):
```csharp
using Microsoft.Extensions.DependencyInjection;
using Oficina.Adaptadores.Catalogo.Controllers;
using Oficina.Adaptadores.Catalogo.Gateways;
using Oficina.Adaptadores.Clientes.Controllers;
using Oficina.Adaptadores.Clientes.Gateways;
using Oficina.Aplicacao.Catalogo.Gateways;
using Oficina.Aplicacao.Clientes.Gateways;

namespace Oficina.Adaptadores;

public static class DependencyInjectionAdaptadores
{
    public static IServiceCollection AdicionarAdaptadores(this IServiceCollection services)
    {
        // Catálogo
        services.AddScoped<IServicoGateway, ServicoGateway>();
        services.AddScoped<ServicoController>();

        // Clientes
        services.AddScoped<IClienteGateway, ClienteGateway>();
        services.AddScoped<ClienteController>();
        return services;
    }
}
```

> Nota: os 10 use cases de Clientes já estão registrados em `Oficina.Aplicacao/DependencyInjectionAplicacao.cs` (Scoped) e **não mudam** — o `ClienteController` os recebe por injeção. Os use cases de OrdensServico/Consulta também continuam registrados lá e agora resolvem `IClienteGateway` (registrado em `AdicionarAdaptadores`), pois `Program.cs` compõe `AdicionarAplicacao()` + `AdicionarAdaptadores()` + `AdicionarInfraestrutura()` no mesmo container.

- [ ] **Step 12: Deixar o controller HTTP fino (delegando ao `ClienteController`)**

Substituir `src/Oficina.Api/Controllers/ClientesController.cs` (rotas, verbos, status e query `documento` preservados exatamente; a action `Buscar` continua combinando busca-por-documento e listagem paginada; `AdicionarVeiculo` continua usando `CreatedAtAction(nameof(ListarVeiculos), new { id }, resp)`):
```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oficina.Adaptadores.Clientes.Controllers;
using Oficina.Aplicacao.Clientes.Dtos;
using Oficina.Api.Configuracao;

namespace Oficina.Api.Controllers;

[ApiController]
[Route("api/v1/clientes")]
[Authorize(Policy = PoliticasDeAutorizacao.RequerAdminOuAtendente)]
public class ClientesController : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Criar(
        [FromBody] CriarClienteRequest req,
        [FromServices] ClienteController controller,
        CancellationToken ct)
    {
        var resp = await controller.CriarAsync(req, ct);
        return CreatedAtAction(nameof(Obter), new { id = resp.Id }, resp);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Obter(
        Guid id,
        [FromServices] ClienteController controller,
        CancellationToken ct)
    {
        var resp = await controller.ObterPorIdAsync(id, ct);
        return resp is null ? NotFound() : Ok(resp);
    }

    [HttpGet]
    public async Task<IActionResult> Buscar(
        [FromServices] ClienteController controller,
        [FromQuery] string? documento = null,
        [FromQuery] int pagina = 1,
        [FromQuery] int tamanhoPagina = 20,
        CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(documento))
        {
            var c = await controller.BuscarPorDocumentoAsync(documento, ct);
            return c is null ? NotFound() : Ok(c);
        }
        var resultado = await controller.ListarAsync(pagina, tamanhoPagina, ct);
        return Ok(resultado);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Atualizar(
        Guid id,
        [FromBody] AtualizarClienteRequest req,
        [FromServices] ClienteController controller,
        CancellationToken ct)
    {
        var resp = await controller.AtualizarAsync(id, req, ct);
        return resp is null ? NotFound() : Ok(resp);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Remover(
        Guid id,
        [FromServices] ClienteController controller,
        CancellationToken ct)
    {
        var ok = await controller.RemoverAsync(id, ct);
        return ok ? NoContent() : NotFound();
    }

    // ===== Veículos =====

    [HttpGet("{id:guid}/veiculos")]
    public async Task<IActionResult> ListarVeiculos(
        Guid id,
        [FromServices] ClienteController controller,
        CancellationToken ct)
    {
        var lista = await controller.ListarVeiculosAsync(id, ct);
        return lista is null ? NotFound() : Ok(lista);
    }

    [HttpPost("{id:guid}/veiculos")]
    public async Task<IActionResult> AdicionarVeiculo(
        Guid id,
        [FromBody] AdicionarVeiculoRequest req,
        [FromServices] ClienteController controller,
        CancellationToken ct)
    {
        var resp = await controller.AdicionarVeiculoAsync(id, req, ct);
        return resp is null
            ? NotFound()
            : CreatedAtAction(nameof(ListarVeiculos), new { id }, resp);
    }

    [HttpPut("{id:guid}/veiculos/{placa}")]
    public async Task<IActionResult> AtualizarVeiculo(
        Guid id,
        string placa,
        [FromBody] AtualizarVeiculoRequest req,
        [FromServices] ClienteController controller,
        CancellationToken ct)
    {
        var resp = await controller.AtualizarVeiculoAsync(id, placa, req, ct);
        return resp is null ? NotFound() : Ok(resp);
    }

    [HttpDelete("{id:guid}/veiculos/{placa}")]
    public async Task<IActionResult> RemoverVeiculo(
        Guid id,
        string placa,
        [FromServices] ClienteController controller,
        CancellationToken ct)
    {
        var ok = await controller.RemoverVeiculoAsync(id, placa, ct);
        return ok ? NoContent() : NotFound();
    }
}
```

- [ ] **Step 13: Adaptar os testes de use case de Clientes (mock `IClienteGateway`; assert em entidades)**

Substituir `tests/Oficina.Aplicacao.Testes/Clientes/CriarClienteUseCaseTestes.cs`:
```csharp
using FluentAssertions;
using Moq;
using Oficina.Aplicacao.Clientes;
using Oficina.Aplicacao.Clientes.Dtos;
using Oficina.Aplicacao.Clientes.Gateways;
using Oficina.Dominio.Clientes;
using Xunit;

namespace Oficina.Aplicacao.Testes.Clientes;

public class CriarClienteUseCaseTestes
{
    private readonly Mock<IClienteGateway> _gateway = new();

    private CriarClienteUseCase Construir() => new(_gateway.Object);

    [Fact]
    public async Task Executar_ComDadosValidos_DeveCriarERetornarEntidade()
    {
        _gateway.Setup(r => r.ExisteDocumentoAsync(It.IsAny<Documento>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var req = new CriarClienteRequest("João", "39053344705", "joao@x.com", "11987654321");
        var cliente = await Construir().ExecutarAsync(req, default);

        cliente.Nome.Should().Be("João");
        cliente.Documento.Valor.Should().Be("39053344705");
        cliente.Documento.Tipo.Should().Be(TipoPessoa.PF);
        _gateway.Verify(r => r.AdicionarAsync(It.IsAny<Cliente>(), It.IsAny<CancellationToken>()), Times.Once);
        _gateway.Verify(r => r.SalvarAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Executar_ComDocumentoJaExistente_DeveLancar()
    {
        _gateway.Setup(r => r.ExisteDocumentoAsync(It.IsAny<Documento>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var req = new CriarClienteRequest("João", "39053344705", "joao@x.com", "11987654321");
        var act = async () => await Construir().ExecutarAsync(req, default);

        await act.Should().ThrowAsync<DocumentoJaCadastradoException>();
    }

    [Fact]
    public async Task Executar_ComDocumentoInvalido_DevePropagarExcecaoDoVO()
    {
        var req = new CriarClienteRequest("João", "11111111111", "joao@x.com", "11987654321");
        var act = async () => await Construir().ExecutarAsync(req, default);

        await act.Should().ThrowAsync<DocumentoInvalidoException>();
    }
}
```

Substituir `tests/Oficina.Aplicacao.Testes/Clientes/BuscarClientePorDocumentoUseCaseTestes.cs` (agora retorna `Cliente?`; ler `Documento.Valor`):
```csharp
using FluentAssertions;
using Moq;
using Oficina.Aplicacao.Clientes;
using Oficina.Aplicacao.Clientes.Gateways;
using Oficina.Dominio.Clientes;
using Xunit;

namespace Oficina.Aplicacao.Testes.Clientes;

public class BuscarClientePorDocumentoUseCaseTestes
{
    private readonly Mock<IClienteGateway> _gateway = new();

    [Fact]
    public async Task Executar_ComDocumentoValidoEExistente_DeveRetornarCliente()
    {
        var c = Cliente.Criar("Joao", Documento.Criar("39053344705"),
            Email.Criar("a@b.com"), Telefone.Criar("11987654321"));

        _gateway.Setup(r => r.ObterPorDocumentoAsync(It.IsAny<Documento>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(c);

        var resp = await new BuscarClientePorDocumentoUseCase(_gateway.Object)
            .ExecutarAsync("390.533.447-05", default);

        resp.Should().NotBeNull();
        resp!.Documento.Valor.Should().Be("39053344705");
    }

    [Fact]
    public async Task Executar_ComDocumentoInexistente_DeveRetornarNull()
    {
        _gateway.Setup(r => r.ObterPorDocumentoAsync(It.IsAny<Documento>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Cliente?)null);

        var resp = await new BuscarClientePorDocumentoUseCase(_gateway.Object)
            .ExecutarAsync("39053344705", default);

        resp.Should().BeNull();
    }

    [Fact]
    public async Task Executar_ComDocumentoInvalido_DevePropagarExcecao()
    {
        var act = async () => await new BuscarClientePorDocumentoUseCase(_gateway.Object)
            .ExecutarAsync("11111111111", default);

        await act.Should().ThrowAsync<DocumentoInvalidoException>();
    }
}
```

Substituir `tests/Oficina.Aplicacao.Testes/Clientes/AdicionarVeiculoUseCaseTestes.cs` (agora retorna `Veiculo?`; ler `Placa.Valor`):
```csharp
using FluentAssertions;
using Moq;
using Oficina.Aplicacao.Clientes;
using Oficina.Aplicacao.Clientes.Dtos;
using Oficina.Aplicacao.Clientes.Gateways;
using Oficina.Dominio.Clientes;
using Xunit;

namespace Oficina.Aplicacao.Testes.Clientes;

public class AdicionarVeiculoUseCaseTestes
{
    private readonly Mock<IClienteGateway> _gateway = new();

    [Fact]
    public async Task Executar_ComClienteExistente_DeveRetornarVeiculo()
    {
        var cliente = Cliente.Criar("J", Documento.Criar("39053344705"),
            Email.Criar("a@b.com"), Telefone.Criar("11987654321"));

        _gateway.Setup(r => r.ObterPorIdAsync(cliente.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cliente);

        var req = new AdicionarVeiculoRequest("ABC1234", "Fiat", "Uno", 2020);
        var resp = await new AdicionarVeiculoUseCase(_gateway.Object).ExecutarAsync(cliente.Id, req, default);

        resp.Should().NotBeNull();
        resp!.Placa.Valor.Should().Be("ABC1234");
        cliente.Veiculos.Should().HaveCount(1);
        _gateway.Verify(r => r.MarcarVeiculoComoNovo(It.IsAny<Veiculo>()), Times.Once);
    }

    [Fact]
    public async Task Executar_ComClienteInexistente_DeveRetornarNull()
    {
        _gateway.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Cliente?)null);

        var req = new AdicionarVeiculoRequest("ABC1234", "Fiat", "Uno", 2020);
        var resp = await new AdicionarVeiculoUseCase(_gateway.Object)
            .ExecutarAsync(Guid.NewGuid(), req, default);

        resp.Should().BeNull();
    }

    [Fact]
    public async Task Executar_ComPlacaJaExistente_DevePropagarExcecao()
    {
        var cliente = Cliente.Criar("J", Documento.Criar("39053344705"),
            Email.Criar("a@b.com"), Telefone.Criar("11987654321"));
        cliente.AdicionarVeiculo(Veiculo.Criar(Placa.Criar("ABC1234"), "F", "U", 2020));

        _gateway.Setup(r => r.ObterPorIdAsync(cliente.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cliente);

        var req = new AdicionarVeiculoRequest("ABC1234", "VW", "Gol", 2018);
        var act = async () => await new AdicionarVeiculoUseCase(_gateway.Object)
            .ExecutarAsync(cliente.Id, req, default);

        await act.Should().ThrowAsync<PlacaJaCadastradaException>();
    }
}
```

Substituir `tests/Oficina.Aplicacao.Testes/Clientes/RemoverVeiculoUseCaseTestes.cs` (só troca o mock; retorno segue `bool`):
```csharp
using FluentAssertions;
using Moq;
using Oficina.Aplicacao.Clientes;
using Oficina.Aplicacao.Clientes.Gateways;
using Oficina.Dominio.Clientes;
using Xunit;

namespace Oficina.Aplicacao.Testes.Clientes;

public class RemoverVeiculoUseCaseTestes
{
    private readonly Mock<IClienteGateway> _gateway = new();

    [Fact]
    public async Task Executar_ComPlacaInexistente_DeveLancar()
    {
        var cliente = Cliente.Criar("J", Documento.Criar("39053344705"),
            Email.Criar("a@b.com"), Telefone.Criar("11987654321"));

        _gateway.Setup(r => r.ObterPorIdAsync(cliente.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cliente);

        var act = async () => await new RemoverVeiculoUseCase(_gateway.Object)
            .ExecutarAsync(cliente.Id, "ZZZ9999", default);

        await act.Should().ThrowAsync<VeiculoNaoEncontradoException>();
    }
}
```

- [ ] **Step 14: Adaptar os testes cross-context (OrdensServico + Consulta)**

Em cada arquivo abaixo, aplicar a **mesma transformação mecânica**: (a) adicionar `using Oficina.Aplicacao.Clientes.Gateways;` (manter `using Oficina.Dominio.Clientes;`); (b) trocar a linha `private readonly Mock<IClienteRepositorio> _clientes = new();` por `private readonly Mock<IClienteGateway> _clientes = new();`. Nenhum cenário muda (todos só fazem `Setup`/uso de `ObterPorIdAsync`, que existe em `IClienteGateway`).

- `tests/Oficina.Aplicacao.Testes/OrdensServico/CriarOrdemUseCaseTestes.cs`
- `tests/Oficina.Aplicacao.Testes/Consulta/ConsultarOrdemPorNumeroUseCaseTestes.cs`
- `tests/Oficina.Aplicacao.Testes/Consulta/AprovarOrcamentoPorClienteUseCaseTestes.cs`

(Não existe teste dedicado para `RejeitarOrcamentoPorClienteUseCase`, então nada a adaptar lá.)

- [ ] **Step 15: Escrever o teste do `ClienteGateway` (delegação para o DataSource)**

Create `tests/Oficina.Adaptadores.Testes/Clientes/ClienteGatewayTestes.cs`:
```csharp
using FluentAssertions;
using Moq;
using Oficina.Adaptadores.Clientes.DataSources;
using Oficina.Adaptadores.Clientes.Gateways;
using Oficina.Dominio.Clientes;

namespace Oficina.Adaptadores.Testes.Clientes;

public class ClienteGatewayTestes
{
    private static Cliente CriarCliente() => Cliente.Criar("João", Documento.Criar("39053344705"),
        Email.Criar("joao@x.com"), Telefone.Criar("11987654321"));

    [Fact]
    public async Task ObterPorIdAsync_DeveDelegarParaODataSource()
    {
        var ds = new Mock<IClienteDataSource>();
        var esperado = CriarCliente();
        ds.Setup(d => d.ObterPorIdAsync(esperado.Id, It.IsAny<CancellationToken>())).ReturnsAsync(esperado);

        var gateway = new ClienteGateway(ds.Object);
        var obtido = await gateway.ObterPorIdAsync(esperado.Id, default);

        obtido.Should().BeSameAs(esperado);
        ds.Verify(d => d.ObterPorIdAsync(esperado.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AdicionarESalvar_DevemDelegarParaODataSource()
    {
        var ds = new Mock<IClienteDataSource>();
        var gateway = new ClienteGateway(ds.Object);
        var cliente = CriarCliente();

        await gateway.AdicionarAsync(cliente, default);
        await gateway.SalvarAsync(default);

        ds.Verify(d => d.AdicionarAsync(cliente, It.IsAny<CancellationToken>()), Times.Once);
        ds.Verify(d => d.SalvarAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void RemoverEMarcarVeiculoComoNovo_DevemDelegarParaODataSource()
    {
        var ds = new Mock<IClienteDataSource>();
        var gateway = new ClienteGateway(ds.Object);
        var cliente = CriarCliente();
        var veiculo = Veiculo.Criar(Placa.Criar("ABC1234"), "Fiat", "Uno", 2020);

        gateway.Remover(cliente);
        gateway.MarcarVeiculoComoNovo(veiculo);

        ds.Verify(d => d.Remover(cliente), Times.Once);
        ds.Verify(d => d.MarcarVeiculoComoNovo(veiculo), Times.Once);
    }
}
```

- [ ] **Step 16: Escrever o teste do `ClienteController` (orquestração + Presenter)**

Create `tests/Oficina.Adaptadores.Testes/Clientes/ClienteControllerTestes.cs`:
```csharp
using FluentAssertions;
using Moq;
using Oficina.Adaptadores.Clientes.Controllers;
using Oficina.Aplicacao.Clientes;
using Oficina.Aplicacao.Clientes.Dtos;
using Oficina.Aplicacao.Clientes.Gateways;
using Oficina.Dominio.Clientes;

namespace Oficina.Adaptadores.Testes.Clientes;

public class ClienteControllerTestes
{
    private static ClienteController CriarController(Mock<IClienteGateway> gateway) =>
        new(
            new CriarClienteUseCase(gateway.Object),
            new ObterClientePorIdUseCase(gateway.Object),
            new BuscarClientePorDocumentoUseCase(gateway.Object),
            new ListarClientesUseCase(gateway.Object),
            new AtualizarClienteUseCase(gateway.Object),
            new RemoverClienteUseCase(gateway.Object),
            new AdicionarVeiculoUseCase(gateway.Object),
            new AtualizarVeiculoUseCase(gateway.Object),
            new RemoverVeiculoUseCase(gateway.Object),
            new ListarVeiculosUseCase(gateway.Object));

    private static Cliente CriarCliente() => Cliente.Criar("João", Documento.Criar("39053344705"),
        Email.Criar("joao@x.com"), Telefone.Criar("11987654321"));

    [Fact]
    public async Task CriarAsync_DevePersistirERetornarClienteResponseFormatado()
    {
        var gateway = new Mock<IClienteGateway>();
        gateway.Setup(g => g.ExisteDocumentoAsync(It.IsAny<Documento>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var controller = CriarController(gateway);

        var resp = await controller.CriarAsync(
            new CriarClienteRequest("João", "39053344705", "joao@x.com", "11987654321"), default);

        resp.Should().BeOfType<ClienteResponse>();
        resp.Nome.Should().Be("João");
        resp.TipoPessoa.Should().Be("PF");
        gateway.Verify(g => g.AdicionarAsync(It.IsAny<Cliente>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ObterPorIdAsync_QuandoNaoExiste_DeveRetornarNull()
    {
        var gateway = new Mock<IClienteGateway>();
        gateway.Setup(g => g.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Cliente?)null);
        var controller = CriarController(gateway);

        var resp = await controller.ObterPorIdAsync(Guid.NewGuid(), default);

        resp.Should().BeNull();
    }

    [Fact]
    public async Task ListarAsync_DeveMontarPaginaClientes()
    {
        var gateway = new Mock<IClienteGateway>();
        gateway.Setup(g => g.ListarAsync(1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { CriarCliente() });
        gateway.Setup(g => g.ContarAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        var controller = CriarController(gateway);

        var pagina = await controller.ListarAsync(1, 20, default);

        pagina.Total.Should().Be(1);
        pagina.Itens.Should().ContainSingle(c => c.Nome == "João");
    }

    [Fact]
    public async Task AdicionarVeiculoAsync_ComClienteExistente_DeveRetornarVeiculoResponse()
    {
        var gateway = new Mock<IClienteGateway>();
        var cliente = CriarCliente();
        gateway.Setup(g => g.ObterPorIdAsync(cliente.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cliente);
        var controller = CriarController(gateway);

        var resp = await controller.AdicionarVeiculoAsync(
            cliente.Id, new AdicionarVeiculoRequest("ABC1234", "Fiat", "Uno", 2020), default);

        resp.Should().NotBeNull();
        resp!.Placa.Should().Be("ABC1234");
        gateway.Verify(g => g.MarcarVeiculoComoNovo(It.IsAny<Veiculo>()), Times.Once);
    }

    [Fact]
    public async Task ListarVeiculosAsync_QuandoClienteNaoExiste_DeveRetornarNull()
    {
        var gateway = new Mock<IClienteGateway>();
        gateway.Setup(g => g.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Cliente?)null);
        var controller = CriarController(gateway);

        var resp = await controller.ListarVeiculosAsync(Guid.NewGuid(), default);

        resp.Should().BeNull();
    }
}
```

- [ ] **Step 17: Verificar que não sobraram referências à interface deletada nem ao mapeador**

Run:
```bash
cd "C:/Users/thiago/source/repos/Fiap/fiap_15SOAT_fase1"
grep -rn "IClienteRepositorio" src tests --include=*.cs
grep -rn "MapeadorClienteResponse" src tests --include=*.cs
```
Expected: **nenhuma linha** de saída em ambos (todas as referências migraram para `IClienteGateway`/`ClientePresenter`). Se algo aparecer (ex.: um caso de uso cross-context ainda não migrado), corrigir migrando para `IClienteGateway` (mesmo assembly `Oficina.Aplicacao`) antes de prosseguir.

- [ ] **Step 18: Compilar a solution**

Run: `dotnet build Oficina.sln`
Expected: **Build succeeded**, 0 erros (todas as referências resolvidas; deleções sem referências pendentes).

- [ ] **Step 19: Rodar os testes unitários — um projeto por chamada (toolchain .NET 10)**

Run (uma chamada por projeto; passar múltiplos projetos falha com `MSB1008`):
```bash
dotnet test tests/Oficina.Dominio.Testes
dotnet test tests/Oficina.Aplicacao.Testes
dotnet test tests/Oficina.Adaptadores.Testes
```
Expected: PASS em todos (inclui os testes adaptados de Clientes/OrdensServico/Consulta e os novos testes de Presenter/Gateway/Controller de Clientes).

- [ ] **Step 20: (CI apenas) Testes de integração — NÃO rodar localmente (Docker indisponível)**

Os testes `tests/Oficina.Integracao.Testes/Clientes/ClientesEndpointTestes.cs` e `.../VeiculosEndpointTestes.cs` são a rede de segurança do contrato HTTP (201/200/204/404/409/422, busca por `documento`, fluxo completo de veículos, `401` sem token). Eles usam Testcontainers + Postgres e rodam **no CI**. Localmente, apenas registrar que o gate local (Steps 18–19) passou; **não** executar `dotnet test tests/Oficina.Integracao.Testes` sem Docker.

- [ ] **Step 21: Commit**

```bash
cd "C:/Users/thiago/source/repos/Fiap/fiap_15SOAT_fase1"
git add -A
git commit -m "$(cat <<'EOF'
refactor: Clientes para Clean Architecture (Gateway/DataSource/Presenter/Controller)

Casos de uso de Clientes passam a consumir IClienteGateway e a retornar
entidades; ClienteController (aplicacao) orquestra e formata via
ClientePresenter; ClienteGateway delega para IClienteDataSource, implementado
por ClienteDataSource (EF Core) — preserva Include(Veiculos),
MarcarVeiculoComoNovo, paginacao com clamp e busca por documento. Controller
HTTP fica fino. Casos de uso de OrdensServico/Consulta migram de
IClienteRepositorio para IClienteGateway. Remove IClienteRepositorio,
ClienteRepositorio e MapeadorClienteResponse. Contrato HTTP inalterado.

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Auto-revisão do plano (cobertura vs. contexto Clientes)

- **Cobertura de arquivos:** os 10 casos de uso de Clientes, os 6 DTOs (inalterados), os 4 validators (inalterados — continuam válidos), o repositório de infra, o controller HTTP e a interface de domínio estão todos endereçados. DTOs e Validators **não mudam** (o contrato e as regras de validação de entrada são idênticos). ✔
- **Particularidades do Clientes vs. template do Catálogo (desvios conscientes):**
  1. **Veiculo aninhado** — o `ClientePresenter` ganha um método extra `ApresentarVeiculo(Veiculo) : VeiculoResponse` (não existe equivalente no Catálogo) e `Apresentar` projeta `Veiculos`. ✔
  2. **`MarcarVeiculoComoNovo` + `Remover`** — métodos `void` presentes em `IClienteRepositorio` são preservados em `IClienteGateway`/`IClienteDataSource` e delegados 1:1 no `ClienteGateway` (o Catálogo não tinha métodos `void`). O comentário-workaround do EF é mantido no `IClienteGateway` e no `ClienteDataSource`. ✔
  3. **Rotas aninhadas** `/{id}/veiculos[/{placa}]` — o `ClienteController` expõe 4 métodos de veículo (`AdicionarVeiculoAsync`, `AtualizarVeiculoAsync`, `RemoverVeiculoAsync`, `ListarVeiculosAsync`) e o HTTP controller preserva os verbos e o `CreatedAtAction(nameof(ListarVeiculos), new { id }, resp)`. ✔
  4. **Busca por `documento` na query** — a action HTTP `Buscar` combina `BuscarPorDocumentoAsync` (404 se não achar) e `ListarAsync` (paginada), preservada exatamente. ✔
  5. **Cross-context em OrdensServico/Consulta** — 4 arquivos-fonte + 3 arquivos-teste migram de `IClienteRepositorio` para `IClienteGateway` (mesmo assembly `Oficina.Aplicacao`); todos só usam `ObterPorIdAsync`. Sem essa migração o build não fica verde após a deleção da interface. Um `grep` de verificação (Step 17) garante que não sobraram referências. ✔
  6. **`ListarClientesUseCase`** já continha o record `PaginaClientes` — ele é **mantido** no mesmo arquivo/namespace `Oficina.Aplicacao.Clientes` (contrato) e ganha o vizinho `ResultadoListaClientes` (entidades), espelhando `PaginaServicos`/`ResultadoListaServicos`. ✔
- **Consistência de tipos/nomes entre passos:** assinaturas de `IClienteGateway` ≡ `IClienteDataSource`; retornos dos use cases (`Cliente`/`Cliente?`/`bool`/`Veiculo`/`Veiculo?`/`IReadOnlyList<Veiculo>?`/`ResultadoListaClientes`) batem com o consumo no `ClienteController` e nos testes; `ClientePresenter.Apresentar` usa exatamente os mesmos campos do antigo `MapeadorClienteResponse.Mapear` (incl. `Documento.Tipo.ToString()`, `Documento.Valor`, `Documento.Mascarado()`, `Email.Valor`, `Telefone.Valor`). O valor mascarado esperado no teste (`390.533.447-05`) foi conferido contra `Documento.Mascarado()` para o CPF `39053344705`. ✔
- **Sem placeholders:** todo código é completo; caminhos e comandos são absolutos/exatos; comandos de teste respeitam a regra "um projeto por chamada". ✔
- **Regra de dependência:** `IClienteGateway` mora em `Oficina.Aplicacao` (para os use cases cross-context poderem consumi-la sem `Oficina.Aplicacao` depender de `Oficina.Adaptadores`); `IClienteDataSource` mora em `Oficina.Adaptadores`; `ClienteDataSource` (Infra) implementa a interface de Adaptadores. ✔
```
