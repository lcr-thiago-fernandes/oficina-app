# Fase 2 — Plano 03: Refactor Clean Architecture — contexto Estoque

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Refatorar o contexto **Estoque** (Peca + MovimentacaoEstoque aninhada) ponta-a-ponta para a Clean Architecture do curso, replicando **exatamente** o padrão já aprovado no Catálogo (Plano 01) e Clientes (Plano 02): Entidades → Casos de Uso (que passam a consumir `IPecaGateway` e a retornar entidades de domínio) → Adaptadores de Interface (`PecaController` + `PecaGateway` + `PecaPresenter`) → Frameworks & Drivers (`PecaDataSource` EF Core em Infraestrutura + `PecasController` HTTP fino). O comportamento externo (contrato HTTP) **não muda**.

**Architecture:** Os 7 casos de uso de Estoque passam a depender de `IPecaGateway` (definida na camada de Casos de Uso, em `Oficina.Aplicacao`) e a **retornar entidades de domínio** (`Peca`/`Peca?`/`bool`/`MovimentacaoEstoque?`/`IReadOnlyList<MovimentacaoEstoque>?` e um `ResultadoListaPecas` para a listagem). Um `PecaController` (Adaptadores) orquestra os casos de uso e usa `PecaPresenter` para formatar a saída nos mesmos DTOs de hoje (`PecaResponse`, `MovimentacaoResponse`, `PaginaPecas`). `PecaGateway` implementa `IPecaGateway` delegando 1:1 para `IPecaDataSource` — **incluindo o `EmTransacaoSerializadaAsync(Func<CancellationToken,Task>, CancellationToken)` e o `MarcarMovimentacaoComoNova(MovimentacaoEstoque)`**. `PecaDataSource` (EF Core, em Infraestrutura) implementa `IPecaDataSource` com o corpo do antigo `PecaRepositorio` (preserva a transação serializável `BeginTransactionAsync(IsolationLevel.Serializable)` + Commit/Rollback/Dispose, o `.Include(Movimentacoes)`, o filtro por SKU/nome com `ILike`, a paginação com clamp, e `MarcarMovimentacaoComoNova` via `_db.Set<MovimentacaoEstoque>().Add`). O controller HTTP `PecasController` fica fino e só delega para o `PecaController` de aplicação, preservando rotas, verbos e status codes (incl. o `Created(string.Empty, resp)` da movimentação). Como `IPecaRepositorio` é deletada, os casos de uso de **OrdensServico** que consomem estoque (`IniciarExecucaoUseCase`, `AdicionarItemPecaUseCase`) migram para `IPecaGateway` (mesmo assembly `Oficina.Aplicacao`).

**Tech Stack:** C# 12 / .NET 8, ASP.NET Core, EF Core 8 + Npgsql, xUnit 2.5.3 + FluentAssertions 6.12.1 + Moq 4.20.72, Testcontainers.PostgreSql (integração). O projeto `Oficina.Adaptadores` e `Oficina.Adaptadores.Testes` **já existem** (Plano 01) — não há task de scaffold.

## Global Constraints

- **Idioma pt-BR** em código, identificadores, comentários e mensagens de commit (convenção do projeto).
- **.NET 8** (`net8.0`), `Nullable=enable`, `ImplicitUsings=enable` em todos os projetos.
- **Regra de dependência** (curso): `Oficina.Api → Oficina.Adaptadores → Oficina.Aplicacao → Oficina.Dominio`; `Oficina.Infraestrutura → Oficina.Adaptadores` (implementa `IPecaDataSource`) + `→ Oficina.Aplicacao` + `→ Oficina.Dominio`; `Oficina.Api → Oficina.Infraestrutura` só para wiring de DI. **`Oficina.Dominio` continua sem dependências externas.** `Oficina.Aplicacao` **não** referencia `Oficina.Adaptadores` — por isso `IPecaGateway` mora em `Oficina.Aplicacao`.
- **DI idiomático**: classes stateless registradas no container (Scoped), sem `new`/`static` manual de dependências (exceto nos testes unitários).
- **Contrato HTTP inalterado**: `PecaResponse`, `MovimentacaoResponse`, `PaginaPecas`, rotas `/api/v1/pecas` (+ aninhada `/{id}/movimentacoes`), verbos, status codes (201/200/204/404/409/422). Em particular: criar peça retorna **201 `CreatedAtAction`**; registrar movimentação retorna **201 `Created(string.Empty, resp)`** (peça inexistente → 404); saldo insuficiente → **422** (via `SaldoInsuficienteException`); SKU duplicado → **409** (via `SkuJaCadastradoException`); tipo de movimentação inválido → **422** (via `MovimentacaoInvalidaException`). O `[Authorize(Policy = RequerAdminOuAtendente)]` é preservado. As exceções de domínio continuam sendo lançadas pelos casos de uso e traduzidas por `MiddlewareDeExcecoes` — **não** tratar exceções no `PecaController` nem no HTTP controller.
- **Docker indisponível no ambiente local** → **NÃO** rodar os testes de integração (`Oficina.Integracao.Testes`); eles rodam no CI. Gate local = `dotnet build Oficina.sln` (0 erros) + os 3 projetos de teste unitários (Domínio, Aplicação, Adaptadores) verdes.
- **Toolchain .NET 10** no ambiente: rodar **um projeto de teste por chamada** de `dotnet test` (passar múltiplos projetos falha com `MSB1008`).
- **Bash tool = Git Bash** (POSIX sh): usar `rm`, `grep` e heredoc `<<'EOF'` — **não** usar sintaxe PowerShell.
- **Cobertura de linha ≥ 80%** no CI (gate). DTO/Request/Response já são excluídos por `coverlet.runsettings`.
- **Branch:** `fase-2`. Commits em pt-BR terminando com:
  `Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>`

## Estrutura de arquivos (o que este plano cria/modifica/deleta)

```
src/Oficina.Aplicacao/
  Estoque/Gateways/IPecaGateway.cs                           ★NOVO (abstração consumida pelos use cases)
  Estoque/CriarPecaUseCase.cs                                MODIFICADO (gateway; retorna Peca)
  Estoque/AtualizarPecaUseCase.cs                            MODIFICADO (gateway; retorna Peca?)
  Estoque/RemoverPecaUseCase.cs                              MODIFICADO (gateway; retorna bool)
  Estoque/ObterPecaPorIdUseCase.cs                           MODIFICADO (gateway; retorna Peca?)
  Estoque/ListarPecasUseCase.cs                              MODIFICADO (gateway; retorna ResultadoListaPecas; mantém PaginaPecas)
  Estoque/RegistrarMovimentacaoUseCase.cs                    MODIFICADO (gateway; retorna MovimentacaoEstoque?; preserva transação)
  Estoque/ListarMovimentacoesUseCase.cs                      MODIFICADO (gateway; retorna IReadOnlyList<MovimentacaoEstoque>?)
  Estoque/MapeadorEstoque.cs                                 DELETADO (vira PecaPresenter)
  OrdensServico/IniciarExecucaoUseCase.cs                    MODIFICADO (IPecaRepositorio → IPecaGateway)
  OrdensServico/AdicionarItemPecaUseCase.cs                  MODIFICADO (IPecaRepositorio → IPecaGateway)

src/Oficina.Adaptadores/
  Estoque/DataSources/IPecaDataSource.cs                     ★NOVO (interface consumida pelo Gateway)
  Estoque/Gateways/PecaGateway.cs                            ★NOVO (IPecaGateway → delega p/ IPecaDataSource)
  Estoque/Presenters/PecaPresenter.cs                        ★NOVO (Peca/MovimentacaoEstoque → Response / PaginaPecas)
  Estoque/Controllers/PecaController.cs                      ★NOVO (orquestra os 7 use cases + Presenter)
  DependencyInjectionAdaptadores.cs                          MODIFICADO (+ IPecaGateway e PecaController)

src/Oficina.Dominio/
  Estoque/IPecaRepositorio.cs                                DELETADO (vira IPecaGateway)

src/Oficina.Infraestrutura/
  Persistencia/DataSources/PecaDataSource.cs                 ★NOVO (EF Core; corpo do antigo repositório, incl. transação serializável)
  Persistencia/Repositorios/PecaRepositorio.cs               DELETADO
  DependencyInjectionRepositorios.cs                         MODIFICADO (troca binding Peca)

src/Oficina.Api/
  Controllers/PecasController.cs                             MODIFICADO (fino; delega ao PecaController)

tests/Oficina.Adaptadores.Testes/
  Estoque/PecaPresenterTestes.cs                             ★NOVO
  Estoque/PecaGatewayTestes.cs                               ★NOVO (inclui teste de delegação de EmTransacaoSerializadaAsync)
  Estoque/PecaControllerTestes.cs                            ★NOVO

tests/Oficina.Aplicacao.Testes/
  Estoque/CriarPecaUseCaseTestes.cs                          MODIFICADO (mock IPecaGateway; assert entidade)
  Estoque/RegistrarMovimentacaoUseCaseTestes.cs              MODIFICADO (mock IPecaGateway; retorno vira MovimentacaoEstoque?)
  OrdensServico/IniciarExecucaoUseCaseTestes.cs              MODIFICADO (Mock<IPecaRepositorio> → Mock<IPecaGateway>)
  OrdensServico/AdicionarItensUseCaseTestes.cs               MODIFICADO (Mock<IPecaRepositorio> → Mock<IPecaGateway>)
```

**Resumo:** 9 arquivos criados, 16 modificados, 3 deletados.

---

## Task 1: Refatorar o contexto Estoque para Clean Architecture

Refatora Estoque ponta-a-ponta. Muitos passos pequenos; o build só fica verde ao final (a deleção de `IPecaRepositorio` quebra tudo até que casos de uso, cross-context, DataSource e DI estejam prontos). Ao final: **build verde + os 3 projetos de teste unitários verdes**, com o contrato HTTP intacto (validado no CI pelos testes de integração).

**Files:**
- Create: `src/Oficina.Aplicacao/Estoque/Gateways/IPecaGateway.cs`
- Create: `src/Oficina.Adaptadores/Estoque/DataSources/IPecaDataSource.cs`
- Create: `src/Oficina.Adaptadores/Estoque/Gateways/PecaGateway.cs`
- Create: `src/Oficina.Adaptadores/Estoque/Presenters/PecaPresenter.cs`
- Create: `src/Oficina.Adaptadores/Estoque/Controllers/PecaController.cs`
- Create: `src/Oficina.Infraestrutura/Persistencia/DataSources/PecaDataSource.cs`
- Create: `tests/Oficina.Adaptadores.Testes/Estoque/PecaPresenterTestes.cs`
- Create: `tests/Oficina.Adaptadores.Testes/Estoque/PecaGatewayTestes.cs`
- Create: `tests/Oficina.Adaptadores.Testes/Estoque/PecaControllerTestes.cs`
- Modify: os 7 use cases de `src/Oficina.Aplicacao/Estoque/`
- Modify: `src/Oficina.Aplicacao/OrdensServico/IniciarExecucaoUseCase.cs`, `src/Oficina.Aplicacao/OrdensServico/AdicionarItemPecaUseCase.cs`
- Modify: `src/Oficina.Api/Controllers/PecasController.cs`
- Modify: `src/Oficina.Infraestrutura/DependencyInjectionRepositorios.cs`
- Modify: `src/Oficina.Adaptadores/DependencyInjectionAdaptadores.cs`
- Modify: `tests/Oficina.Aplicacao.Testes/Estoque/{CriarPeca,RegistrarMovimentacao}UseCaseTestes.cs`
- Modify: `tests/Oficina.Aplicacao.Testes/OrdensServico/{IniciarExecucaoUseCaseTestes,AdicionarItensUseCaseTestes}.cs`
- Delete: `src/Oficina.Dominio/Estoque/IPecaRepositorio.cs`, `src/Oficina.Infraestrutura/Persistencia/Repositorios/PecaRepositorio.cs`, `src/Oficina.Aplicacao/Estoque/MapeadorEstoque.cs`

**Interfaces (padrão idêntico ao Catálogo/Clientes):**
- `Oficina.Aplicacao.Estoque.Gateways.IPecaGateway` — mesma forma do antigo `IPecaRepositorio` (inclui `EmTransacaoSerializadaAsync(Func<CancellationToken,Task>, CancellationToken)` e `MarcarMovimentacaoComoNova(MovimentacaoEstoque)`).
- `Oficina.Adaptadores.Estoque.DataSources.IPecaDataSource` — mesma forma.
- `Oficina.Adaptadores.Estoque.Gateways.PecaGateway : IPecaGateway`.
- `Oficina.Adaptadores.Estoque.Presenters.PecaPresenter` (estático): `Apresentar(Peca) : PecaResponse`, `ApresentarMovimentacao(MovimentacaoEstoque) : MovimentacaoResponse`, `ApresentarPagina(IReadOnlyList<Peca>, int, int, int) : PaginaPecas`.
- `Oficina.Adaptadores.Estoque.Controllers.PecaController` — `CriarAsync`, `ObterPorIdAsync`, `ListarAsync`, `AtualizarAsync`, `RemoverAsync`, `RegistrarMovimentacaoAsync`, `ListarMovimentacoesAsync`.
- Use cases retornam entidade: `CriarPecaUseCase → Task<Peca>`, `ObterPecaPorIdUseCase → Task<Peca?>`, `AtualizarPecaUseCase → Task<Peca?>`, `RemoverPecaUseCase → Task<bool>`, `ListarPecasUseCase → Task<ResultadoListaPecas>`, `RegistrarMovimentacaoUseCase → Task<MovimentacaoEstoque?>`, `ListarMovimentacoesUseCase → Task<IReadOnlyList<MovimentacaoEstoque>?>`.
- `Oficina.Aplicacao.Estoque.ResultadoListaPecas(IReadOnlyList<Peca> Itens, int Total, int Pagina, int TamanhoPagina)`.
- `Oficina.Aplicacao.Estoque.PaginaPecas(IReadOnlyList<PecaResponse> Itens, int Total, int Pagina, int TamanhoPagina)` — **mantida no namespace `Oficina.Aplicacao.Estoque`** (contrato inalterado).

---

- [ ] **Step 1: Criar `IPecaGateway` (camada de Casos de Uso)**

Create `src/Oficina.Aplicacao/Estoque/Gateways/IPecaGateway.cs`:
```csharp
using Oficina.Dominio.Estoque;

namespace Oficina.Aplicacao.Estoque.Gateways;

public interface IPecaGateway
{
    Task<Peca?> ObterPorIdAsync(Guid id, CancellationToken ct);
    Task<Peca?> ObterPorSkuAsync(Sku sku, CancellationToken ct);
    Task<bool> ExisteSkuAsync(Sku sku, CancellationToken ct);
    Task<IReadOnlyList<Peca>> ListarAsync(string? filtroNome, int pagina, int tamanhoPagina, bool incluirInativos, CancellationToken ct);
    Task<int> ContarAsync(string? filtroNome, bool incluirInativos, CancellationToken ct);
    Task AdicionarAsync(Peca peca, CancellationToken ct);
    Task SalvarAsync(CancellationToken ct);

    /// <summary>
    /// Executa uma operação em transação serializável (para garantir
    /// consistência de saldo sob concorrência).
    /// </summary>
    Task EmTransacaoSerializadaAsync(Func<CancellationToken, Task> acao, CancellationToken ct);

    // EF Core nao detecta entidades adicionadas via navigation collection
    // como Added quando o Id ja vem preenchido — gera UPDATE em vez de
    // INSERT. Marca a movimentacao como Added explicitamente.
    void MarcarMovimentacaoComoNova(MovimentacaoEstoque movimentacao);
}
```

- [ ] **Step 2: Refatorar os 7 casos de uso de Estoque (depender do gateway; retornar entidades)**

Substituir `src/Oficina.Aplicacao/Estoque/CriarPecaUseCase.cs`:
```csharp
using Oficina.Aplicacao.Estoque.Dtos;
using Oficina.Aplicacao.Estoque.Gateways;
using Oficina.Dominio.Estoque;

namespace Oficina.Aplicacao.Estoque;

public class CriarPecaUseCase
{
    private readonly IPecaGateway _gateway;
    public CriarPecaUseCase(IPecaGateway gateway) => _gateway = gateway;

    public async Task<Peca> ExecutarAsync(CriarPecaRequest req, CancellationToken ct)
    {
        var sku = Sku.Criar(req.Sku);

        if (await _gateway.ExisteSkuAsync(sku, ct))
            throw new SkuJaCadastradoException(sku.Valor);

        var peca = Peca.Criar(sku, req.Nome, req.PrecoUnitario);
        await _gateway.AdicionarAsync(peca, ct);
        await _gateway.SalvarAsync(ct);

        return peca;
    }
}
```

Substituir `src/Oficina.Aplicacao/Estoque/AtualizarPecaUseCase.cs`:
```csharp
using Oficina.Aplicacao.Estoque.Dtos;
using Oficina.Aplicacao.Estoque.Gateways;
using Oficina.Dominio.Estoque;

namespace Oficina.Aplicacao.Estoque;

public class AtualizarPecaUseCase
{
    private readonly IPecaGateway _gateway;
    public AtualizarPecaUseCase(IPecaGateway gateway) => _gateway = gateway;

    public async Task<Peca?> ExecutarAsync(Guid id, AtualizarPecaRequest req, CancellationToken ct)
    {
        var p = await _gateway.ObterPorIdAsync(id, ct);
        if (p is null) return null;

        p.AtualizarDados(req.Nome, req.PrecoUnitario);
        await _gateway.SalvarAsync(ct);
        return p;
    }
}
```

Substituir `src/Oficina.Aplicacao/Estoque/RemoverPecaUseCase.cs`:
```csharp
using Oficina.Aplicacao.Estoque.Gateways;

namespace Oficina.Aplicacao.Estoque;

public class RemoverPecaUseCase
{
    private readonly IPecaGateway _gateway;
    public RemoverPecaUseCase(IPecaGateway gateway) => _gateway = gateway;

    public async Task<bool> ExecutarAsync(Guid id, CancellationToken ct)
    {
        var p = await _gateway.ObterPorIdAsync(id, ct);
        if (p is null) return false;

        p.Inativar(); // soft delete
        await _gateway.SalvarAsync(ct);
        return true;
    }
}
```

Substituir `src/Oficina.Aplicacao/Estoque/ObterPecaPorIdUseCase.cs`:
```csharp
using Oficina.Aplicacao.Estoque.Gateways;
using Oficina.Dominio.Estoque;

namespace Oficina.Aplicacao.Estoque;

public class ObterPecaPorIdUseCase
{
    private readonly IPecaGateway _gateway;
    public ObterPecaPorIdUseCase(IPecaGateway gateway) => _gateway = gateway;

    public Task<Peca?> ExecutarAsync(Guid id, CancellationToken ct) => _gateway.ObterPorIdAsync(id, ct);
}
```

Substituir `src/Oficina.Aplicacao/Estoque/ListarPecasUseCase.cs`:
```csharp
using Oficina.Aplicacao.Estoque.Dtos;
using Oficina.Aplicacao.Estoque.Gateways;
using Oficina.Dominio.Estoque;

namespace Oficina.Aplicacao.Estoque;

// Response DTO consumido pelo Presenter e pelo cliente HTTP (mantido neste namespace por compatibilidade).
public sealed record PaginaPecas(IReadOnlyList<PecaResponse> Itens, int Total, int Pagina, int TamanhoPagina);

// Resultado do use case em termos de entidades de domínio (o Presenter converte em PaginaPecas).
public sealed record ResultadoListaPecas(IReadOnlyList<Peca> Itens, int Total, int Pagina, int TamanhoPagina);

public class ListarPecasUseCase
{
    private readonly IPecaGateway _gateway;
    public ListarPecasUseCase(IPecaGateway gateway) => _gateway = gateway;

    public async Task<ResultadoListaPecas> ExecutarAsync(string? nome, int pagina, int tamanho, bool incluirInativos, CancellationToken ct)
    {
        var lista = await _gateway.ListarAsync(nome, pagina, tamanho, incluirInativos, ct);
        var total = await _gateway.ContarAsync(nome, incluirInativos, ct);
        return new ResultadoListaPecas(lista, total, pagina, tamanho);
    }
}
```

Substituir `src/Oficina.Aplicacao/Estoque/RegistrarMovimentacaoUseCase.cs` (preserva o fluxo transacional via `_gateway.EmTransacaoSerializadaAsync`; passa a retornar a entidade `MovimentacaoEstoque?`):
```csharp
using Oficina.Aplicacao.Estoque.Dtos;
using Oficina.Aplicacao.Estoque.Gateways;
using Oficina.Dominio.Estoque;

namespace Oficina.Aplicacao.Estoque;

public class RegistrarMovimentacaoUseCase
{
    private readonly IPecaGateway _gateway;
    public RegistrarMovimentacaoUseCase(IPecaGateway gateway) => _gateway = gateway;

    public async Task<MovimentacaoEstoque?> ExecutarAsync(
        Guid pecaId,
        RegistrarMovimentacaoRequest req,
        CancellationToken ct)
    {
        var tipo = req.Tipo switch
        {
            "Entrada" => TipoMovimentacao.Entrada,
            "Saida" => TipoMovimentacao.Saida,
            _ => throw new MovimentacaoInvalidaException(
                "Tipo de movimentação inválido. Use 'Entrada' ou 'Saida'.")
        };

        MovimentacaoEstoque? mov = null;

        await _gateway.EmTransacaoSerializadaAsync(async tx =>
        {
            var peca = await _gateway.ObterPorIdAsync(pecaId, tx);
            if (peca is null) return;

            mov = tipo == TipoMovimentacao.Entrada
                ? peca.RegistrarEntrada(req.Quantidade, req.Motivo)
                : peca.RegistrarSaida(req.Quantidade, req.Motivo, req.OrdemServicoId);
            _gateway.MarcarMovimentacaoComoNova(mov);

            await _gateway.SalvarAsync(tx);
        }, ct);

        return mov;
    }
}
```

Substituir `src/Oficina.Aplicacao/Estoque/ListarMovimentacoesUseCase.cs` (mantém a ordenação `CriadoEm` desc; retorna entidades):
```csharp
using Oficina.Aplicacao.Estoque.Gateways;
using Oficina.Dominio.Estoque;

namespace Oficina.Aplicacao.Estoque;

public class ListarMovimentacoesUseCase
{
    private readonly IPecaGateway _gateway;
    public ListarMovimentacoesUseCase(IPecaGateway gateway) => _gateway = gateway;

    public async Task<IReadOnlyList<MovimentacaoEstoque>?> ExecutarAsync(Guid pecaId, CancellationToken ct)
    {
        var p = await _gateway.ObterPorIdAsync(pecaId, ct);
        if (p is null) return null;
        return p.Movimentacoes
            .OrderByDescending(m => m.CriadoEm)
            .ToList();
    }
}
```

- [ ] **Step 3: Deletar o mapeador da Aplicação (vira Presenter)**

Run:
```bash
cd "C:/Users/thiago/source/repos/Fiap/fiap_15SOAT_fase1"
rm src/Oficina.Aplicacao/Estoque/MapeadorEstoque.cs
```

- [ ] **Step 4: Criar `IPecaDataSource` e `PecaGateway` (Adaptadores)**

Create `src/Oficina.Adaptadores/Estoque/DataSources/IPecaDataSource.cs`:
```csharp
using Oficina.Dominio.Estoque;

namespace Oficina.Adaptadores.Estoque.DataSources;

public interface IPecaDataSource
{
    Task<Peca?> ObterPorIdAsync(Guid id, CancellationToken ct);
    Task<Peca?> ObterPorSkuAsync(Sku sku, CancellationToken ct);
    Task<bool> ExisteSkuAsync(Sku sku, CancellationToken ct);
    Task<IReadOnlyList<Peca>> ListarAsync(string? filtroNome, int pagina, int tamanhoPagina, bool incluirInativos, CancellationToken ct);
    Task<int> ContarAsync(string? filtroNome, bool incluirInativos, CancellationToken ct);
    Task AdicionarAsync(Peca peca, CancellationToken ct);
    Task SalvarAsync(CancellationToken ct);
    Task EmTransacaoSerializadaAsync(Func<CancellationToken, Task> acao, CancellationToken ct);
    void MarcarMovimentacaoComoNova(MovimentacaoEstoque movimentacao);
}
```

Create `src/Oficina.Adaptadores/Estoque/Gateways/PecaGateway.cs`:
```csharp
using Oficina.Adaptadores.Estoque.DataSources;
using Oficina.Aplicacao.Estoque.Gateways;
using Oficina.Dominio.Estoque;

namespace Oficina.Adaptadores.Estoque.Gateways;

public class PecaGateway : IPecaGateway
{
    private readonly IPecaDataSource _dataSource;
    public PecaGateway(IPecaDataSource dataSource) => _dataSource = dataSource;

    public Task<Peca?> ObterPorIdAsync(Guid id, CancellationToken ct) =>
        _dataSource.ObterPorIdAsync(id, ct);

    public Task<Peca?> ObterPorSkuAsync(Sku sku, CancellationToken ct) =>
        _dataSource.ObterPorSkuAsync(sku, ct);

    public Task<bool> ExisteSkuAsync(Sku sku, CancellationToken ct) =>
        _dataSource.ExisteSkuAsync(sku, ct);

    public Task<IReadOnlyList<Peca>> ListarAsync(string? filtroNome, int pagina, int tamanhoPagina, bool incluirInativos, CancellationToken ct) =>
        _dataSource.ListarAsync(filtroNome, pagina, tamanhoPagina, incluirInativos, ct);

    public Task<int> ContarAsync(string? filtroNome, bool incluirInativos, CancellationToken ct) =>
        _dataSource.ContarAsync(filtroNome, incluirInativos, ct);

    public Task AdicionarAsync(Peca peca, CancellationToken ct) =>
        _dataSource.AdicionarAsync(peca, ct);

    public Task SalvarAsync(CancellationToken ct) =>
        _dataSource.SalvarAsync(ct);

    public Task EmTransacaoSerializadaAsync(Func<CancellationToken, Task> acao, CancellationToken ct) =>
        _dataSource.EmTransacaoSerializadaAsync(acao, ct);

    public void MarcarMovimentacaoComoNova(MovimentacaoEstoque movimentacao) =>
        _dataSource.MarcarMovimentacaoComoNova(movimentacao);
}
```

- [ ] **Step 5: Criar `PecaPresenter` (Adaptadores)**

Create `src/Oficina.Adaptadores/Estoque/Presenters/PecaPresenter.cs`:
```csharp
using Oficina.Aplicacao.Estoque;
using Oficina.Aplicacao.Estoque.Dtos;
using Oficina.Dominio.Estoque;

namespace Oficina.Adaptadores.Estoque.Presenters;

public static class PecaPresenter
{
    public static PecaResponse Apresentar(Peca p) =>
        new(p.Id, p.Sku.Valor, p.Nome, p.PrecoUnitario, p.SaldoAtual, p.Ativo, p.CriadoEm);

    public static MovimentacaoResponse ApresentarMovimentacao(MovimentacaoEstoque m) =>
        new(m.Id, m.Tipo.ToString(), m.Quantidade, m.Motivo, m.OrdemServicoId, m.CriadoEm);

    public static PaginaPecas ApresentarPagina(IReadOnlyList<Peca> itens, int total, int pagina, int tamanhoPagina) =>
        new(itens.Select(Apresentar).ToList(), total, pagina, tamanhoPagina);
}
```

- [ ] **Step 6: Escrever o teste do `PecaPresenter`**

Create `tests/Oficina.Adaptadores.Testes/Estoque/PecaPresenterTestes.cs`:
```csharp
using System.Linq;
using FluentAssertions;
using Oficina.Adaptadores.Estoque.Presenters;
using Oficina.Dominio.Estoque;

namespace Oficina.Adaptadores.Testes.Estoque;

public class PecaPresenterTestes
{
    private static Peca CriarPecaComMovimentacao()
    {
        var peca = Peca.Criar(Sku.Criar("ABC-123"), "Filtro", 25m);
        peca.RegistrarEntrada(10, "compra inicial");
        return peca;
    }

    [Fact]
    public void Apresentar_DeveMapearTodosOsCamposDaEntidade()
    {
        var peca = CriarPecaComMovimentacao();

        var resp = PecaPresenter.Apresentar(peca);

        resp.Id.Should().Be(peca.Id);
        resp.Sku.Should().Be("ABC-123");
        resp.Nome.Should().Be("Filtro");
        resp.PrecoUnitario.Should().Be(25m);
        resp.SaldoAtual.Should().Be(10);
        resp.Ativo.Should().BeTrue();
    }

    [Fact]
    public void ApresentarMovimentacao_DeveMapearCampos()
    {
        var peca = CriarPecaComMovimentacao();
        var mov = peca.Movimentacoes.First();

        var resp = PecaPresenter.ApresentarMovimentacao(mov);

        resp.Id.Should().Be(mov.Id);
        resp.Tipo.Should().Be("Entrada");
        resp.Quantidade.Should().Be(10);
        resp.Motivo.Should().Be("compra inicial");
        resp.OrdemServicoId.Should().BeNull();
    }

    [Fact]
    public void ApresentarPagina_DeveMapearItensEMetadados()
    {
        var itens = new[] { CriarPecaComMovimentacao() };

        var pagina = PecaPresenter.ApresentarPagina(itens, total: 1, pagina: 1, tamanhoPagina: 20);

        pagina.Total.Should().Be(1);
        pagina.Pagina.Should().Be(1);
        pagina.TamanhoPagina.Should().Be(20);
        pagina.Itens.Should().ContainSingle(p => p.Sku == "ABC-123");
    }
}
```

- [ ] **Step 7: Criar o `PecaController` de aplicação (Adaptadores)**

Create `src/Oficina.Adaptadores/Estoque/Controllers/PecaController.cs`:
```csharp
using Oficina.Adaptadores.Estoque.Presenters;
using Oficina.Aplicacao.Estoque;
using Oficina.Aplicacao.Estoque.Dtos;

namespace Oficina.Adaptadores.Estoque.Controllers;

// Controller de aplicação (Adaptadores de Interface): orquestra os casos de uso e formata via Presenter.
public class PecaController
{
    private readonly CriarPecaUseCase _criar;
    private readonly ObterPecaPorIdUseCase _obter;
    private readonly ListarPecasUseCase _listar;
    private readonly AtualizarPecaUseCase _atualizar;
    private readonly RemoverPecaUseCase _remover;
    private readonly RegistrarMovimentacaoUseCase _registrarMovimentacao;
    private readonly ListarMovimentacoesUseCase _listarMovimentacoes;

    public PecaController(
        CriarPecaUseCase criar,
        ObterPecaPorIdUseCase obter,
        ListarPecasUseCase listar,
        AtualizarPecaUseCase atualizar,
        RemoverPecaUseCase remover,
        RegistrarMovimentacaoUseCase registrarMovimentacao,
        ListarMovimentacoesUseCase listarMovimentacoes)
    {
        _criar = criar;
        _obter = obter;
        _listar = listar;
        _atualizar = atualizar;
        _remover = remover;
        _registrarMovimentacao = registrarMovimentacao;
        _listarMovimentacoes = listarMovimentacoes;
    }

    public async Task<PecaResponse> CriarAsync(CriarPecaRequest req, CancellationToken ct)
    {
        var peca = await _criar.ExecutarAsync(req, ct);
        return PecaPresenter.Apresentar(peca);
    }

    public async Task<PecaResponse?> ObterPorIdAsync(Guid id, CancellationToken ct)
    {
        var peca = await _obter.ExecutarAsync(id, ct);
        return peca is null ? null : PecaPresenter.Apresentar(peca);
    }

    public async Task<PaginaPecas> ListarAsync(string? nome, int pagina, int tamanhoPagina, bool incluirInativos, CancellationToken ct)
    {
        var r = await _listar.ExecutarAsync(nome, pagina, tamanhoPagina, incluirInativos, ct);
        return PecaPresenter.ApresentarPagina(r.Itens, r.Total, r.Pagina, r.TamanhoPagina);
    }

    public async Task<PecaResponse?> AtualizarAsync(Guid id, AtualizarPecaRequest req, CancellationToken ct)
    {
        var peca = await _atualizar.ExecutarAsync(id, req, ct);
        return peca is null ? null : PecaPresenter.Apresentar(peca);
    }

    public Task<bool> RemoverAsync(Guid id, CancellationToken ct) => _remover.ExecutarAsync(id, ct);

    public async Task<MovimentacaoResponse?> RegistrarMovimentacaoAsync(Guid pecaId, RegistrarMovimentacaoRequest req, CancellationToken ct)
    {
        var mov = await _registrarMovimentacao.ExecutarAsync(pecaId, req, ct);
        return mov is null ? null : PecaPresenter.ApresentarMovimentacao(mov);
    }

    public async Task<IReadOnlyList<MovimentacaoResponse>?> ListarMovimentacoesAsync(Guid pecaId, CancellationToken ct)
    {
        var movs = await _listarMovimentacoes.ExecutarAsync(pecaId, ct);
        return movs?.Select(PecaPresenter.ApresentarMovimentacao).ToList();
    }
}
```

- [ ] **Step 8: Criar o `PecaDataSource` (Infraestrutura) com o corpo do antigo repositório**

**CRÍTICO:** copiar o corpo do antigo `PecaRepositorio` EXATAMENTE — em especial `EmTransacaoSerializadaAsync` (`BeginTransactionAsync(IsolationLevel.Serializable)` + Commit/Rollback/Dispose), o `.Include(p => p.Movimentacoes)`, o filtro por SKU (`Sku.Valor` em variável local) e por nome (`EF.Functions.ILike`), a paginação com clamp e `MarcarMovimentacaoComoNova` (`_db.Set<MovimentacaoEstoque>().Add`).

Create `src/Oficina.Infraestrutura/Persistencia/DataSources/PecaDataSource.cs`:
```csharp
using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Oficina.Adaptadores.Estoque.DataSources;
using Oficina.Dominio.Estoque;

namespace Oficina.Infraestrutura.Persistencia.DataSources;

public class PecaDataSource : IPecaDataSource
{
    private readonly OficinaDbContext _db;

    public PecaDataSource(OficinaDbContext db) => _db = db;

    public Task<Peca?> ObterPorIdAsync(Guid id, CancellationToken ct) =>
        _db.Pecas.Include(p => p.Movimentacoes).FirstOrDefaultAsync(p => p.Id == id, ct);

    public Task<Peca?> ObterPorSkuAsync(Sku sku, CancellationToken ct)
    {
        var v = sku.Valor;
        return _db.Pecas.Include(p => p.Movimentacoes)
            .FirstOrDefaultAsync(p => p.Sku.Valor == v, ct);
    }

    public Task<bool> ExisteSkuAsync(Sku sku, CancellationToken ct)
    {
        var v = sku.Valor;
        return _db.Pecas.AnyAsync(p => p.Sku.Valor == v, ct);
    }

    public async Task<IReadOnlyList<Peca>> ListarAsync(
        string? filtroNome, int pagina, int tamanhoPagina, bool incluirInativos, CancellationToken ct)
    {
        if (pagina < 1) pagina = 1;
        if (tamanhoPagina < 1 || tamanhoPagina > 100) tamanhoPagina = 20;

        var q = _db.Pecas.AsQueryable();
        if (!incluirInativos) q = q.Where(p => p.Ativo);
        if (!string.IsNullOrWhiteSpace(filtroNome))
            q = q.Where(p => EF.Functions.ILike(p.Nome, $"%{filtroNome}%"));

        return await q
            .OrderBy(p => p.Nome)
            .Skip((pagina - 1) * tamanhoPagina)
            .Take(tamanhoPagina)
            .ToListAsync(ct);
    }

    public Task<int> ContarAsync(string? filtroNome, bool incluirInativos, CancellationToken ct)
    {
        var q = _db.Pecas.AsQueryable();
        if (!incluirInativos) q = q.Where(p => p.Ativo);
        if (!string.IsNullOrWhiteSpace(filtroNome))
            q = q.Where(p => EF.Functions.ILike(p.Nome, $"%{filtroNome}%"));
        return q.CountAsync(ct);
    }

    public async Task AdicionarAsync(Peca peca, CancellationToken ct) =>
        await _db.Pecas.AddAsync(peca, ct);

    public Task SalvarAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);

    public void MarcarMovimentacaoComoNova(MovimentacaoEstoque movimentacao) =>
        _db.Set<MovimentacaoEstoque>().Add(movimentacao);

    public async Task EmTransacaoSerializadaAsync(Func<CancellationToken, Task> acao, CancellationToken ct)
    {
        IDbContextTransaction? tx = null;
        try
        {
            tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            await acao(ct);
            await tx.CommitAsync(ct);
        }
        catch
        {
            if (tx is not null) await tx.RollbackAsync(ct);
            throw;
        }
        finally
        {
            if (tx is not null) await tx.DisposeAsync();
        }
    }
}
```

- [ ] **Step 9: Deletar o antigo repositório e a antiga interface de repositório de Estoque**

Run:
```bash
cd "C:/Users/thiago/source/repos/Fiap/fiap_15SOAT_fase1"
rm src/Oficina.Infraestrutura/Persistencia/Repositorios/PecaRepositorio.cs
rm src/Oficina.Dominio/Estoque/IPecaRepositorio.cs
```

- [ ] **Step 10: Migrar os casos de uso cross-context (OrdensServico) para `IPecaGateway`**

Estes dois casos de uso consomem estoque via `_pecas`. Vivem no assembly `Oficina.Aplicacao`, então podem referenciar `Oficina.Aplicacao.Estoque.Gateways.IPecaGateway`. Ambos usam apenas métodos que existem em `IPecaGateway`: `IniciarExecucaoUseCase` usa `_pecas.ObterPorIdAsync` + `_pecas.MarcarMovimentacaoComoNova` (a transação serializável usada por ele é a do repositório de **ordens**, `_ordens.EmTransacaoSerializadaAsync`, que **não** muda); `AdicionarItemPecaUseCase` usa apenas `_pecas.ObterPorIdAsync`. Nenhum dos dois referencia mais nada de `Oficina.Dominio.Estoque` por tipo explícito (as variáveis são inferidas com `var`), então o `using Oficina.Dominio.Estoque;` é **substituído** por `using Oficina.Aplicacao.Estoque.Gateways;`.

Substituir `src/Oficina.Aplicacao/OrdensServico/IniciarExecucaoUseCase.cs`:
```csharp
using Oficina.Aplicacao.Estoque.Gateways;
using Oficina.Aplicacao.OrdensServico.Dtos;
using Oficina.Dominio.OrdensServico;

namespace Oficina.Aplicacao.OrdensServico;

public class IniciarExecucaoUseCase
{
    private readonly IOrdemDeServicoRepositorio _ordens;
    private readonly IPecaGateway _pecas;

    public IniciarExecucaoUseCase(IOrdemDeServicoRepositorio ordens, IPecaGateway pecas)
    {
        _ordens = ordens;
        _pecas = pecas;
    }

    public async Task<OrdemResponse?> ExecutarAsync(Guid ordemId, CancellationToken ct)
    {
        OrdemResponse? resposta = null;

        await _ordens.EmTransacaoSerializadaAsync(async tx =>
        {
            var ordem = await _ordens.ObterPorIdAsync(ordemId, tx);
            if (ordem is null) return;

            // 1) muda estado da OS — pode lançar OrcamentoNaoAprovadoException ou TransicaoInvalida
            ordem.IniciarExecucao();

            // 2) baixar estoque para cada item de peça (mesma transação)
            foreach (var item in ordem.ItensPeca)
            {
                var peca = await _pecas.ObterPorIdAsync(item.PecaId, tx)
                    ?? throw new OrdemInvalidaException(
                        $"Peça {item.PecaId} referenciada na OS não existe mais.");

                var mov = peca.RegistrarSaida(
                    quantidade: item.Quantidade,
                    motivo: $"OS #{ordem.Numero}",
                    ordemServicoId: ordem.Id);
                _pecas.MarcarMovimentacaoComoNova(mov);
            }

            // 3) persiste tudo na mesma transação
            await _ordens.SalvarAsync(tx);
            ordem.LimparEventos();

            resposta = MapeadorOrdem.Mapear(ordem);
        }, ct);

        return resposta;
    }
}
```

Substituir `src/Oficina.Aplicacao/OrdensServico/AdicionarItemPecaUseCase.cs`:
```csharp
using Oficina.Aplicacao.Estoque.Gateways;
using Oficina.Aplicacao.OrdensServico.Dtos;
using Oficina.Dominio.OrdensServico;

namespace Oficina.Aplicacao.OrdensServico;

public class AdicionarItemPecaUseCase
{
    private readonly IOrdemDeServicoRepositorio _ordens;
    private readonly IPecaGateway _pecas;

    public AdicionarItemPecaUseCase(IOrdemDeServicoRepositorio ordens, IPecaGateway pecas)
    {
        _ordens = ordens;
        _pecas = pecas;
    }

    public async Task<ItemPecaResponse?> ExecutarAsync(Guid ordemId, AdicionarItemPecaRequest req, CancellationToken ct)
    {
        var ordem = await _ordens.ObterPorIdAsync(ordemId, ct);
        if (ordem is null) return null;

        var peca = await _pecas.ObterPorIdAsync(req.PecaId, ct)
            ?? throw new OrdemInvalidaException("Peça não encontrada.");
        if (!peca.Ativo)
            throw new OrdemInvalidaException("Peça inativa.");

        var item = ordem.AdicionarItemPeca(peca.Id, peca.Nome, peca.PrecoUnitario, req.Quantidade);
        _ordens.MarcarItemPecaComoNovo(item);
        await _ordens.SalvarAsync(ct);

        return MapeadorOrdem.MapearPeca(item);
    }
}
```

- [ ] **Step 11: Ajustar o wiring de DI (Infra e Adaptadores)**

Substituir `src/Oficina.Infraestrutura/DependencyInjectionRepositorios.cs` (remove o binding `IPecaRepositorio`/`PecaRepositorio` e o `using Oficina.Dominio.Estoque;` que só servia a ele; adiciona o `IPecaDataSource`):
```csharp
using Microsoft.Extensions.DependencyInjection;
using Oficina.Adaptadores.Catalogo.DataSources;
using Oficina.Adaptadores.Clientes.DataSources;
using Oficina.Adaptadores.Estoque.DataSources;
using Oficina.Dominio.Auth;
using Oficina.Dominio.OrdensServico;
using Oficina.Infraestrutura.Persistencia.DataSources;
using Oficina.Infraestrutura.Persistencia.Repositorios;

namespace Oficina.Infraestrutura;

public static class DependencyInjectionRepositorios
{
    public static IServiceCollection AdicionarRepositorios(this IServiceCollection services)
    {
        services.AddScoped<IUsuarioRepositorio, UsuarioRepositorio>();
        services.AddScoped<IOrdemDeServicoRepositorio, OrdemDeServicoRepositorio>();

        // DataSources (Clean Architecture — Frameworks & Drivers)
        services.AddScoped<IServicoDataSource, ServicoDataSource>();
        services.AddScoped<IClienteDataSource, ClienteDataSource>();
        services.AddScoped<IPecaDataSource, PecaDataSource>();
        return services;
    }
}
```

Substituir `src/Oficina.Adaptadores/DependencyInjectionAdaptadores.cs` (adiciona o Gateway e o Controller de Estoque):
```csharp
using Microsoft.Extensions.DependencyInjection;
using Oficina.Adaptadores.Catalogo.Controllers;
using Oficina.Adaptadores.Catalogo.Gateways;
using Oficina.Adaptadores.Clientes.Controllers;
using Oficina.Adaptadores.Clientes.Gateways;
using Oficina.Adaptadores.Estoque.Controllers;
using Oficina.Adaptadores.Estoque.Gateways;
using Oficina.Aplicacao.Catalogo.Gateways;
using Oficina.Aplicacao.Clientes.Gateways;
using Oficina.Aplicacao.Estoque.Gateways;

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

        // Estoque
        services.AddScoped<IPecaGateway, PecaGateway>();
        services.AddScoped<PecaController>();
        return services;
    }
}
```

> Nota: os 7 use cases de Estoque já estão registrados em `Oficina.Aplicacao/DependencyInjectionAplicacao.cs` (Scoped) e **não mudam** — o `PecaController` os recebe por injeção. Os use cases de OrdensServico (`IniciarExecucaoUseCase`, `AdicionarItemPecaUseCase`) também continuam registrados lá e agora resolvem `IPecaGateway` (registrado em `AdicionarAdaptadores`), pois `Program.cs` compõe `AdicionarAplicacao()` + `AdicionarAdaptadores()` + `AdicionarInfraestrutura()` no mesmo container.

- [ ] **Step 12: Deixar o controller HTTP fino (delegando ao `PecaController`)**

Substituir `src/Oficina.Api/Controllers/PecasController.cs` (rotas, verbos e status preservados exatamente; em especial `Created(string.Empty, resp)` na movimentação e o `CreatedAtAction(nameof(Obter), new { id }, resp)` na criação):
```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oficina.Adaptadores.Estoque.Controllers;
using Oficina.Aplicacao.Estoque.Dtos;
using Oficina.Api.Configuracao;

namespace Oficina.Api.Controllers;

[ApiController]
[Route("api/v1/pecas")]
[Authorize(Policy = PoliticasDeAutorizacao.RequerAdminOuAtendente)]
public class PecasController : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Criar(
        [FromBody] CriarPecaRequest req,
        [FromServices] PecaController controller,
        CancellationToken ct)
    {
        var resp = await controller.CriarAsync(req, ct);
        return CreatedAtAction(nameof(Obter), new { id = resp.Id }, resp);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Obter(
        Guid id,
        [FromServices] PecaController controller,
        CancellationToken ct)
    {
        var resp = await controller.ObterPorIdAsync(id, ct);
        return resp is null ? NotFound() : Ok(resp);
    }

    [HttpGet]
    public async Task<IActionResult> Listar(
        [FromServices] PecaController controller,
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
        [FromBody] AtualizarPecaRequest req,
        [FromServices] PecaController controller,
        CancellationToken ct)
    {
        var resp = await controller.AtualizarAsync(id, req, ct);
        return resp is null ? NotFound() : Ok(resp);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Remover(
        Guid id,
        [FromServices] PecaController controller,
        CancellationToken ct)
    {
        var ok = await controller.RemoverAsync(id, ct);
        return ok ? NoContent() : NotFound();
    }

    // ===== Movimentações =====

    [HttpPost("{id:guid}/movimentacoes")]
    public async Task<IActionResult> RegistrarMovimentacao(
        Guid id,
        [FromBody] RegistrarMovimentacaoRequest req,
        [FromServices] PecaController controller,
        CancellationToken ct)
    {
        var resp = await controller.RegistrarMovimentacaoAsync(id, req, ct);
        return resp is null ? NotFound() : Created(string.Empty, resp);
    }

    [HttpGet("{id:guid}/movimentacoes")]
    public async Task<IActionResult> ListarMovimentacoes(
        Guid id,
        [FromServices] PecaController controller,
        CancellationToken ct)
    {
        var resp = await controller.ListarMovimentacoesAsync(id, ct);
        return resp is null ? NotFound() : Ok(resp);
    }
}
```

- [ ] **Step 13: Adaptar os testes de use case de Estoque (mock `IPecaGateway`; assert em entidades)**

Substituir `tests/Oficina.Aplicacao.Testes/Estoque/CriarPecaUseCaseTestes.cs` (agora retorna `Peca`; ler `Sku.Valor` e `SaldoAtual`):
```csharp
using FluentAssertions;
using Moq;
using Oficina.Aplicacao.Estoque;
using Oficina.Aplicacao.Estoque.Dtos;
using Oficina.Aplicacao.Estoque.Gateways;
using Oficina.Dominio.Estoque;
using Xunit;

namespace Oficina.Aplicacao.Testes.Estoque;

public class CriarPecaUseCaseTestes
{
    private readonly Mock<IPecaGateway> _gateway = new();

    [Fact]
    public async Task Executar_ComDadosValidos_DeveCriarERetornar()
    {
        _gateway.Setup(r => r.ExisteSkuAsync(It.IsAny<Sku>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var peca = await new CriarPecaUseCase(_gateway.Object)
            .ExecutarAsync(new CriarPecaRequest("ABC-123", "Filtro", 25m), default);

        peca.Sku.Valor.Should().Be("ABC-123");
        peca.SaldoAtual.Should().Be(0);
        _gateway.Verify(r => r.AdicionarAsync(It.IsAny<Peca>(), It.IsAny<CancellationToken>()), Times.Once);
        _gateway.Verify(r => r.SalvarAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Executar_ComSkuJaExistente_DeveLancar()
    {
        _gateway.Setup(r => r.ExisteSkuAsync(It.IsAny<Sku>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var act = async () => await new CriarPecaUseCase(_gateway.Object)
            .ExecutarAsync(new CriarPecaRequest("ABC-123", "X", 10m), default);

        await act.Should().ThrowAsync<SkuJaCadastradoException>();
    }
}
```

Substituir `tests/Oficina.Aplicacao.Testes/Estoque/RegistrarMovimentacaoUseCaseTestes.cs` (troca o mock e o `using`; o retorno vira `MovimentacaoEstoque?` — os asserts de `NotBeNull`/`BeNull` e de saldo seguem válidos):
```csharp
using FluentAssertions;
using Moq;
using Oficina.Aplicacao.Estoque;
using Oficina.Aplicacao.Estoque.Dtos;
using Oficina.Aplicacao.Estoque.Gateways;
using Oficina.Dominio.Estoque;
using Xunit;

namespace Oficina.Aplicacao.Testes.Estoque;

public class RegistrarMovimentacaoUseCaseTestes
{
    private readonly Mock<IPecaGateway> _gateway = new();

    private void ConfigurarTransacaoIdentidade()
    {
        _gateway.Setup(r => r.EmTransacaoSerializadaAsync(
                It.IsAny<Func<CancellationToken, Task>>(),
                It.IsAny<CancellationToken>()))
            .Returns<Func<CancellationToken, Task>, CancellationToken>((f, ct) => f(ct));
    }

    [Fact]
    public async Task Executar_Entrada_DeveAumentarSaldoEPersistir()
    {
        var p = Peca.Criar(Sku.Criar("ABC-123"), "Filtro", 10m);
        _gateway.Setup(r => r.ObterPorIdAsync(p.Id, It.IsAny<CancellationToken>())).ReturnsAsync(p);
        ConfigurarTransacaoIdentidade();

        var resp = await new RegistrarMovimentacaoUseCase(_gateway.Object).ExecutarAsync(
            p.Id, new RegistrarMovimentacaoRequest("Entrada", 5, "Compra", null), default);

        resp.Should().NotBeNull();
        p.SaldoAtual.Should().Be(5);
        _gateway.Verify(r => r.SalvarAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Executar_SaidaComSaldoSuficiente_DeveDiminuir()
    {
        var p = Peca.Criar(Sku.Criar("ABC-123"), "Filtro", 10m);
        p.RegistrarEntrada(10, "compra inicial");
        _gateway.Setup(r => r.ObterPorIdAsync(p.Id, It.IsAny<CancellationToken>())).ReturnsAsync(p);
        ConfigurarTransacaoIdentidade();

        var resp = await new RegistrarMovimentacaoUseCase(_gateway.Object).ExecutarAsync(
            p.Id, new RegistrarMovimentacaoRequest("Saida", 3, "OS", Guid.NewGuid()), default);

        resp.Should().NotBeNull();
        p.SaldoAtual.Should().Be(7);
    }

    [Fact]
    public async Task Executar_SaidaComSaldoInsuficiente_DeveLancar()
    {
        var p = Peca.Criar(Sku.Criar("ABC-123"), "Filtro", 10m);
        p.RegistrarEntrada(2, "compra");
        _gateway.Setup(r => r.ObterPorIdAsync(p.Id, It.IsAny<CancellationToken>())).ReturnsAsync(p);
        ConfigurarTransacaoIdentidade();

        var act = async () => await new RegistrarMovimentacaoUseCase(_gateway.Object).ExecutarAsync(
            p.Id, new RegistrarMovimentacaoRequest("Saida", 5, "x", null), default);

        await act.Should().ThrowAsync<SaldoInsuficienteException>();
        p.SaldoAtual.Should().Be(2); // não mexeu
    }

    [Fact]
    public async Task Executar_PecaInexistente_DeveRetornarNull()
    {
        _gateway.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Peca?)null);
        ConfigurarTransacaoIdentidade();

        var resp = await new RegistrarMovimentacaoUseCase(_gateway.Object).ExecutarAsync(
            Guid.NewGuid(), new RegistrarMovimentacaoRequest("Entrada", 1, "x", null), default);

        resp.Should().BeNull();
    }

    [Fact]
    public async Task Executar_TipoInvalido_DeveLancar()
    {
        ConfigurarTransacaoIdentidade();
        var act = async () => await new RegistrarMovimentacaoUseCase(_gateway.Object).ExecutarAsync(
            Guid.NewGuid(), new RegistrarMovimentacaoRequest("Bagulho", 1, "x", null), default);

        await act.Should().ThrowAsync<MovimentacaoInvalidaException>();
    }
}
```

- [ ] **Step 14: Adaptar os testes cross-context (OrdensServico)**

Em cada arquivo abaixo, aplicar a **mesma transformação mecânica**: (a) adicionar `using Oficina.Aplicacao.Estoque.Gateways;` (manter `using Oficina.Dominio.Estoque;` — ambos usam `Peca`/`Sku`/`SaldoInsuficienteException` por tipo explícito); (b) trocar `Mock<IPecaRepositorio>` por `Mock<IPecaGateway>`. Nenhum cenário muda (ambos só usam `ObterPorIdAsync` e, no `IniciarExecucaoUseCase`, `MarcarMovimentacaoComoNova` — que existem em `IPecaGateway`; a transação identidade continua sobre `_ordens`).

Em `tests/Oficina.Aplicacao.Testes/OrdensServico/IniciarExecucaoUseCaseTestes.cs`:
- Adicionar (junto aos demais `using`): `using Oficina.Aplicacao.Estoque.Gateways;`
- Trocar `private readonly Mock<IPecaRepositorio> _pecas = new();` por `private readonly Mock<IPecaGateway> _pecas = new();`

Em `tests/Oficina.Aplicacao.Testes/OrdensServico/AdicionarItensUseCaseTestes.cs`:
- Adicionar (junto aos demais `using`): `using Oficina.Aplicacao.Estoque.Gateways;`
- Trocar `var pecas = new Mock<IPecaRepositorio>();` por `var pecas = new Mock<IPecaGateway>();` (no teste `AdicionarItemPeca_DeveTirarSnapshotDoNomeEPreco`)

- [ ] **Step 15: Escrever o teste do `PecaGateway` (delegação para o DataSource, incl. `EmTransacaoSerializadaAsync`)**

Create `tests/Oficina.Adaptadores.Testes/Estoque/PecaGatewayTestes.cs`:
```csharp
using FluentAssertions;
using Moq;
using Oficina.Adaptadores.Estoque.DataSources;
using Oficina.Adaptadores.Estoque.Gateways;
using Oficina.Dominio.Estoque;

namespace Oficina.Adaptadores.Testes.Estoque;

public class PecaGatewayTestes
{
    private static Peca CriarPeca() => Peca.Criar(Sku.Criar("ABC-123"), "Filtro", 25m);

    [Fact]
    public async Task ObterPorIdAsync_DeveDelegarParaODataSource()
    {
        var ds = new Mock<IPecaDataSource>();
        var esperado = CriarPeca();
        ds.Setup(d => d.ObterPorIdAsync(esperado.Id, It.IsAny<CancellationToken>())).ReturnsAsync(esperado);

        var gateway = new PecaGateway(ds.Object);
        var obtido = await gateway.ObterPorIdAsync(esperado.Id, default);

        obtido.Should().BeSameAs(esperado);
        ds.Verify(d => d.ObterPorIdAsync(esperado.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AdicionarESalvar_DevemDelegarParaODataSource()
    {
        var ds = new Mock<IPecaDataSource>();
        var gateway = new PecaGateway(ds.Object);
        var peca = CriarPeca();

        await gateway.AdicionarAsync(peca, default);
        await gateway.SalvarAsync(default);

        ds.Verify(d => d.AdicionarAsync(peca, It.IsAny<CancellationToken>()), Times.Once);
        ds.Verify(d => d.SalvarAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void MarcarMovimentacaoComoNova_DeveDelegarParaODataSource()
    {
        var ds = new Mock<IPecaDataSource>();
        var gateway = new PecaGateway(ds.Object);
        var peca = CriarPeca();
        var mov = peca.RegistrarEntrada(5, "compra");

        gateway.MarcarMovimentacaoComoNova(mov);

        ds.Verify(d => d.MarcarMovimentacaoComoNova(mov), Times.Once);
    }

    [Fact]
    public async Task EmTransacaoSerializadaAsync_DeveDelegarParaODataSource()
    {
        var ds = new Mock<IPecaDataSource>();
        Func<CancellationToken, Task> acao = _ => Task.CompletedTask;
        ds.Setup(d => d.EmTransacaoSerializadaAsync(acao, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var gateway = new PecaGateway(ds.Object);
        await gateway.EmTransacaoSerializadaAsync(acao, default);

        ds.Verify(d => d.EmTransacaoSerializadaAsync(acao, It.IsAny<CancellationToken>()), Times.Once);
    }
}
```

- [ ] **Step 16: Escrever o teste do `PecaController` (orquestração + Presenter, incl. fluxo de movimentação)**

Create `tests/Oficina.Adaptadores.Testes/Estoque/PecaControllerTestes.cs`:
```csharp
using FluentAssertions;
using Moq;
using Oficina.Adaptadores.Estoque.Controllers;
using Oficina.Aplicacao.Estoque;
using Oficina.Aplicacao.Estoque.Dtos;
using Oficina.Aplicacao.Estoque.Gateways;
using Oficina.Dominio.Estoque;

namespace Oficina.Adaptadores.Testes.Estoque;

public class PecaControllerTestes
{
    private static PecaController CriarController(Mock<IPecaGateway> gateway) =>
        new(
            new CriarPecaUseCase(gateway.Object),
            new ObterPecaPorIdUseCase(gateway.Object),
            new ListarPecasUseCase(gateway.Object),
            new AtualizarPecaUseCase(gateway.Object),
            new RemoverPecaUseCase(gateway.Object),
            new RegistrarMovimentacaoUseCase(gateway.Object),
            new ListarMovimentacoesUseCase(gateway.Object));

    private static void ConfigurarTransacaoIdentidade(Mock<IPecaGateway> gateway) =>
        gateway.Setup(g => g.EmTransacaoSerializadaAsync(
                It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>()))
            .Returns<Func<CancellationToken, Task>, CancellationToken>((f, ct) => f(ct));

    [Fact]
    public async Task CriarAsync_DevePersistirERetornarPecaResponseFormatado()
    {
        var gateway = new Mock<IPecaGateway>();
        gateway.Setup(g => g.ExisteSkuAsync(It.IsAny<Sku>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var controller = CriarController(gateway);

        var resp = await controller.CriarAsync(new CriarPecaRequest("ABC-123", "Filtro", 25m), default);

        resp.Should().BeOfType<PecaResponse>();
        resp.Sku.Should().Be("ABC-123");
        resp.SaldoAtual.Should().Be(0);
        gateway.Verify(g => g.AdicionarAsync(It.IsAny<Peca>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ObterPorIdAsync_QuandoNaoExiste_DeveRetornarNull()
    {
        var gateway = new Mock<IPecaGateway>();
        gateway.Setup(g => g.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Peca?)null);
        var controller = CriarController(gateway);

        var resp = await controller.ObterPorIdAsync(Guid.NewGuid(), default);

        resp.Should().BeNull();
    }

    [Fact]
    public async Task ListarAsync_DeveMontarPaginaPecas()
    {
        var gateway = new Mock<IPecaGateway>();
        var itens = new[] { Peca.Criar(Sku.Criar("ABC-123"), "Filtro", 25m) };
        gateway.Setup(g => g.ListarAsync(null, 1, 20, false, It.IsAny<CancellationToken>())).ReturnsAsync(itens);
        gateway.Setup(g => g.ContarAsync(null, false, It.IsAny<CancellationToken>())).ReturnsAsync(1);
        var controller = CriarController(gateway);

        var pagina = await controller.ListarAsync(null, 1, 20, false, default);

        pagina.Total.Should().Be(1);
        pagina.Itens.Should().ContainSingle(p => p.Sku == "ABC-123");
    }

    [Fact]
    public async Task RegistrarMovimentacaoAsync_ComEntrada_DeveRetornarMovimentacaoResponse()
    {
        var gateway = new Mock<IPecaGateway>();
        var peca = Peca.Criar(Sku.Criar("ABC-123"), "Filtro", 25m);
        gateway.Setup(g => g.ObterPorIdAsync(peca.Id, It.IsAny<CancellationToken>())).ReturnsAsync(peca);
        ConfigurarTransacaoIdentidade(gateway);
        var controller = CriarController(gateway);

        var resp = await controller.RegistrarMovimentacaoAsync(
            peca.Id, new RegistrarMovimentacaoRequest("Entrada", 5, "Compra", null), default);

        resp.Should().NotBeNull();
        resp!.Tipo.Should().Be("Entrada");
        resp.Quantidade.Should().Be(5);
        gateway.Verify(g => g.MarcarMovimentacaoComoNova(It.IsAny<MovimentacaoEstoque>()), Times.Once);
    }

    [Fact]
    public async Task RegistrarMovimentacaoAsync_QuandoPecaNaoExiste_DeveRetornarNull()
    {
        var gateway = new Mock<IPecaGateway>();
        gateway.Setup(g => g.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Peca?)null);
        ConfigurarTransacaoIdentidade(gateway);
        var controller = CriarController(gateway);

        var resp = await controller.RegistrarMovimentacaoAsync(
            Guid.NewGuid(), new RegistrarMovimentacaoRequest("Entrada", 1, "x", null), default);

        resp.Should().BeNull();
    }

    [Fact]
    public async Task ListarMovimentacoesAsync_QuandoPecaNaoExiste_DeveRetornarNull()
    {
        var gateway = new Mock<IPecaGateway>();
        gateway.Setup(g => g.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Peca?)null);
        var controller = CriarController(gateway);

        var resp = await controller.ListarMovimentacoesAsync(Guid.NewGuid(), default);

        resp.Should().BeNull();
    }
}
```

- [ ] **Step 17: Verificar que não sobraram referências à interface deletada nem ao mapeador**

Run:
```bash
cd "C:/Users/thiago/source/repos/Fiap/fiap_15SOAT_fase1"
grep -rn "IPecaRepositorio\|MapeadorEstoque" src tests --include=*.cs
```
Expected: **nenhuma linha** de saída (todas as referências migraram para `IPecaGateway`/`PecaPresenter`). Se algo aparecer (ex.: um caso de uso cross-context ainda não migrado), corrigir migrando para `IPecaGateway` (mesmo assembly `Oficina.Aplicacao`) antes de prosseguir.

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
Expected: PASS em todos (inclui os testes adaptados de Estoque/OrdensServico e os novos testes de Presenter/Gateway/Controller de Estoque).

- [ ] **Step 20: (CI apenas) Testes de integração — NÃO rodar localmente (Docker indisponível)**

Os testes `tests/Oficina.Integracao.Testes/Estoque/PecasEndpointTestes.cs` (contrato HTTP: 201/200/204/404/409/422, `Created(string.Empty, ...)` na movimentação, saldo insuficiente, SKU duplicado, listagem de movimentações) e `tests/Oficina.Integracao.Testes/OrdensServico/OrdensServicoFluxoTestes.cs` (que exercita o `IniciarExecucaoUseCase` com baixa de estoque transacional) são a rede de segurança do refactor. Eles usam Testcontainers + Postgres e rodam **no CI**. Localmente, apenas registrar que o gate local (Steps 18–19) passou; **não** executar `dotnet test tests/Oficina.Integracao.Testes` sem Docker.

- [ ] **Step 21: Commit**

```bash
cd "C:/Users/thiago/source/repos/Fiap/fiap_15SOAT_fase1"
git add -A
git commit -m "$(cat <<'EOF'
refactor: Estoque para Clean Architecture (Gateway/DataSource/Presenter/Controller)

Casos de uso de Estoque passam a consumir IPecaGateway e a retornar entidades;
PecaController (aplicacao) orquestra e formata via PecaPresenter; PecaGateway
delega para IPecaDataSource, implementado por PecaDataSource (EF Core) — preserva
a transacao serializavel (EmTransacaoSerializadaAsync), Include(Movimentacoes),
filtro por SKU/nome, paginacao com clamp e MarcarMovimentacaoComoNova. Controller
HTTP fica fino (preserva Created(string.Empty) na movimentacao). Casos de uso de
OrdensServico (IniciarExecucao, AdicionarItemPeca) migram de IPecaRepositorio para
IPecaGateway. Remove IPecaRepositorio, PecaRepositorio e MapeadorEstoque.
Contrato HTTP inalterado.

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Auto-revisão do plano (cobertura vs. contexto Estoque)

- **Cobertura de arquivos:** os 7 casos de uso de Estoque, os 5 DTOs (inalterados), os 3 validators (inalterados — continuam válidos), o repositório de infra, o controller HTTP, a interface de domínio e o mapeador estão todos endereçados. DTOs e Validators **não mudam** (contrato e regras de validação de entrada idênticos). Os 2 casos de uso cross-context (`IniciarExecucaoUseCase`, `AdicionarItemPecaUseCase`) e seus testes migram junto. ✔
- **Particularidades do Estoque vs. template do Catálogo/Clientes (desvios conscientes):**
  1. **`EmTransacaoSerializadaAsync(Func<CancellationToken,Task>, CancellationToken)`** — método presente em `IPecaRepositorio` (que o Catálogo/Clientes não tinham). Preservado em `IPecaGateway`/`IPecaDataSource`, delegado 1:1 no `PecaGateway`, e implementado no `PecaDataSource` com o corpo EXATO do antigo repositório (`BeginTransactionAsync(IsolationLevel.Serializable)` + Commit/Rollback/Dispose, usando `System.Data`, `Microsoft.EntityFrameworkCore` e `Microsoft.EntityFrameworkCore.Storage`). Há um teste dedicado (`PecaGatewayTestes.EmTransacaoSerializadaAsync_DeveDelegarParaODataSource`). ✔
  2. **`MovimentacaoEstoque` aninhada + `MarcarMovimentacaoComoNova`** — o `PecaPresenter` ganha `ApresentarMovimentacao(MovimentacaoEstoque) : MovimentacaoResponse` (análogo ao `ApresentarVeiculo` de Clientes); o método `void MarcarMovimentacaoComoNova` é preservado em toda a cadeia (Gateway/DataSource) e usado tanto por `RegistrarMovimentacaoUseCase` quanto por `IniciarExecucaoUseCase`. ✔
  3. **`RegistrarMovimentacaoUseCase` transacional** — o use case mantém a transação via `_gateway.EmTransacaoSerializadaAsync`; passa a retornar a entidade `MovimentacaoEstoque?` (em vez de `MovimentacaoResponse?`); o `PecaController` converte via `PecaPresenter.ApresentarMovimentacao`. O switch de parsing do `Tipo` ("Entrada"/"Saida" → `MovimentacaoInvalidaException`) fica no use case. ✔
  4. **`Created(string.Empty, resp)` na movimentação** — o HTTP controller preserva EXATAMENTE o `Created(string.Empty, resp)` (201 sem Location resolvível), diferente do `CreatedAtAction` usado na criação de peça — ambos preservados. Peça inexistente → 404 (retorno `null` propagado). ✔
  5. **Cross-context em OrdensServico** — 2 arquivos-fonte + 2 arquivos-teste migram de `IPecaRepositorio` para `IPecaGateway` (mesmo assembly `Oficina.Aplicacao`). `IniciarExecucaoUseCase` usa `_pecas.ObterPorIdAsync` + `_pecas.MarcarMovimentacaoComoNova` (a transação serializável dele é a de **ordens**, inalterada); `AdicionarItemPecaUseCase` usa só `_pecas.ObterPorIdAsync`. Sem essa migração o build não fica verde após a deleção da interface. O `grep` de verificação (Step 17) garante que não sobraram referências. ✔
  6. **`ListarPecasUseCase`** já continha o record `PaginaPecas` — ele é **mantido** no mesmo arquivo/namespace `Oficina.Aplicacao.Estoque` (contrato) e ganha o vizinho `ResultadoListaPecas` (entidades), espelhando `PaginaServicos`/`ResultadoListaServicos` e `PaginaClientes`/`ResultadoListaClientes`. ✔
- **Consistência de tipos/nomes entre passos:** assinaturas de `IPecaGateway` ≡ `IPecaDataSource` (incl. `EmTransacaoSerializadaAsync` e `MarcarMovimentacaoComoNova`); retornos dos use cases (`Peca`/`Peca?`/`bool`/`MovimentacaoEstoque?`/`IReadOnlyList<MovimentacaoEstoque>?`/`ResultadoListaPecas`) batem com o consumo no `PecaController` e nos testes; `PecaPresenter.Apresentar` usa exatamente os mesmos campos do antigo `MapeadorEstoque.Mapear` (`Sku.Valor`, `PrecoUnitario`, `SaldoAtual`, `Ativo`, `CriadoEm`) e `ApresentarMovimentacao` os de `MapeadorEstoque.MapearMov` (`Tipo.ToString()`, `Quantidade`, `Motivo`, `OrdemServicoId`, `CriadoEm`). Os asserts adaptados em `CriarPecaUseCaseTestes` passaram de `resp.Sku`→`peca.Sku.Valor` (agora entidade). ✔
- **Sem placeholders:** todo código é completo; caminhos e comandos são absolutos/exatos; comandos de teste respeitam a regra "um projeto por chamada". ✔
- **Regra de dependência:** `IPecaGateway` mora em `Oficina.Aplicacao` (para os use cases cross-context poderem consumi-la sem `Oficina.Aplicacao` depender de `Oficina.Adaptadores`); `IPecaDataSource` mora em `Oficina.Adaptadores`; `PecaDataSource` (Infra) implementa a interface de Adaptadores. `Oficina.Dominio` continua sem dependências externas. ✔
- **DI:** os 7 use cases de Estoque já estão registrados em `DependencyInjectionAplicacao.cs` (verificado — linhas 39-46) e não mudam; `AdicionarAdaptadores` ganha `IPecaGateway→PecaGateway` + `PecaController`; `AdicionarRepositorios` troca `IPecaRepositorio→PecaRepositorio` por `IPecaDataSource→PecaDataSource`. ✔
