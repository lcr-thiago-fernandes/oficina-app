# Fase 2 — Plano 04: Refactor Clean Architecture — contexto Ordens de Serviço (núcleo)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Refatorar o contexto **Ordens de Serviço** (OrdemDeServico + ItemServico/ItemPeca aninhados) ponta-a-ponta para a Clean Architecture do curso, replicando **exatamente** o padrão já aprovado no Catálogo (Plano 01), Clientes (Plano 02) e Estoque (Plano 03), **e** removendo antes os Domain Events mortos. O comportamento externo (contrato HTTP) **não muda**.

**Architecture:** Duas partes sequenciais. **(A)** Remoção do código morto de Domain Events em `OrdemDeServico` (nunca despachados; só criados e limpos): campo `_eventos`, propriedade `EventosNaoPublicados`, método `LimparEventos()`, os `Add` nos métodos de transição, os records `EventoOs`/derivados em `ExcecoesOrdemServico.cs`, o `b.Ignore(x => x.EventosNaoPublicados)` na configuração EF e as chamadas `LimparEventos()` nos use cases. **(B)** Os 13 casos de uso de Ordens de Serviço passam a depender de `IOrdemDeServicoGateway` (definida na camada de Casos de Uso, em `Oficina.Aplicacao`) e a **retornar entidades de domínio** (`OrdemDeServico`/`OrdemDeServico?`/`bool`/`ItemServico?`/`ItemPeca?`/`ResultadoListaOrdens`/`MetricaTempoMedio`). Um `OrdemDeServicoController` (Adaptadores) orquestra os casos de uso e usa `OrdemDeServicoPresenter` para formatar a saída nos mesmos DTOs de hoje (`OrdemResponse`, `ItemServicoResponse`, `ItemPecaResponse`, `PaginaOrdens`, `MetricasTempoMedioResponse`). `OrdemDeServicoGateway` implementa `IOrdemDeServicoGateway` delegando 1:1 para `IOrdemDeServicoDataSource` — **incluindo `EmTransacaoSerializadaAsync`, `ObterTempoMedioExecucaoAsync`, `MarcarItemServicoComoNovo`, `MarcarItemPecaComoNovo` e `ObterPorNumeroAsync`**. `OrdemDeServicoDataSource` (EF Core, em Infraestrutura) implementa `IOrdemDeServicoDataSource` com o corpo do antigo `OrdemDeServicoRepositorio` (preserva `.Include(ItensServico).Include(ItensPeca)`, a transação serializável `BeginTransactionAsync(IsolationLevel.Serializable)` + Commit/Rollback/Dispose, o `ObterTempoMedioExecucaoAsync` com cálculo client-side, `MarcarItem*ComoNovo`, o filtro por status e a paginação com clamp). O record `MetricaTempoMedio` (hoje dentro de `IOrdemDeServicoRepositorio.cs`) muda de lar para o namespace do gateway (`Oficina.Aplicacao.OrdensServico.Gateways`). A conversão `TimeSpan → double minutos` (hoje em `ObterTempoMedioExecucaoUseCase`) migra para `OrdemDeServicoPresenter.ApresentarMetrica`. O controller HTTP `OrdensServicoController` fica fino e só delega, preservando rotas, verbos e status codes. Como `IOrdemDeServicoRepositorio` é deletada, os 3 casos de uso de **Consulta** (cross-context) migram para `IOrdemDeServicoGateway` (mesmo assembly `Oficina.Aplicacao`).

**Tech Stack:** C# 12 / .NET 8, ASP.NET Core, EF Core 8 + Npgsql, xUnit 2.5.3 + FluentAssertions 6.12.1 + Moq 4.20.72, Testcontainers.PostgreSql (integração). O projeto `Oficina.Adaptadores` e `Oficina.Adaptadores.Testes` **já existem** (Plano 01) — não há task de scaffold.

## Global Constraints

- **Idioma pt-BR** em código, identificadores, comentários e mensagens de commit (convenção do projeto).
- **.NET 8** (`net8.0`), `Nullable=enable`, `ImplicitUsings=enable` em todos os projetos. Não há `TreatWarningsAsErrors` (verificado em `Directory.Build.props` e nos `.csproj`) — `using` remanescentes não quebram o build, mas mantenha os arquivos limpos.
- **Regra de dependência** (curso): `Oficina.Api → Oficina.Adaptadores → Oficina.Aplicacao → Oficina.Dominio`; `Oficina.Infraestrutura → Oficina.Adaptadores` (implementa `IOrdemDeServicoDataSource`) + `→ Oficina.Aplicacao` (usa `MetricaTempoMedio`) + `→ Oficina.Dominio`; `Oficina.Api → Oficina.Infraestrutura` só para wiring de DI. **`Oficina.Dominio` continua sem dependências externas.** `Oficina.Aplicacao` **não** referencia `Oficina.Adaptadores` — por isso `IOrdemDeServicoGateway` (e o record `MetricaTempoMedio`) moram em `Oficina.Aplicacao`.
- **DI idiomático**: classes stateless registradas no container (Scoped), sem `new`/`static` manual de dependências (exceto nos testes unitários).
- **Contrato HTTP inalterado**: `OrdemResponse`, `ItemServicoResponse`, `ItemPecaResponse`, `PaginaOrdens`, `MetricasTempoMedioResponse`; rotas `/api/v1/ordens-servico` — `POST` (201 `CreatedAtAction`), `GET` (lista), `GET {id}` (200/404), `PATCH {id}/diagnostico`, `POST {id}/orcamento/enviar`, `POST {id}/execucao/iniciar`, `POST {id}/finalizar`, `POST {id}/entregar` (todos 200/404), `POST/DELETE {id}/servicos[/{itemId}]` e `POST/DELETE {id}/pecas[/{itemId}]` (POST → **201 `Created(string.Empty, resp)`** ou 404; DELETE → 204/404), `GET metricas/tempo-medio` (com `[Authorize(Policy = RequerAdmin)]`). Política de classe: `[Authorize(Policy = RequerAdminOuAtendente)]`. As exceções de domínio (`TransicaoDeStatusInvalidaException`, `OrdemSemItensException`, `OrcamentoNaoAprovadoException`, `OrdemImutavelException`, `ItemNaoEncontradoException`, `ItemInvalidoException`, `SaldoInsuficienteException`, `OrdemInvalidaException`) continuam sendo lançadas pelos casos de uso e traduzidas por `MiddlewareDeExcecoes` — **não** tratar exceções no `OrdemDeServicoController` nem no HTTP controller.
- **Rota pública Consulta inalterada**: `ConsultaController` (`/api/v1/consulta`, `[AllowAnonymous]`) **não muda de contrato**. Os 3 use cases mudam só a dependência interna (`IOrdemDeServicoRepositorio` → `IOrdemDeServicoGateway`); a montagem inline de `ConsultaPublicaResponse` e o comportamento anti-enumeração (404 idêntico para OS inexistente e documento que não confere) são preservados.
- **Docker indisponível no ambiente local** → **NÃO** rodar os testes de integração (`Oficina.Integracao.Testes`); eles rodam no CI. Gate local = `dotnet build Oficina.sln` (0 erros) + os 3 projetos de teste unitários (Domínio, Aplicação, Adaptadores) verdes.
- **Toolchain .NET 10** no ambiente: rodar **um projeto de teste por chamada** de `dotnet test` (passar múltiplos projetos falha com `MSB1008`).
- **Bash tool = Git Bash** (POSIX sh): usar `rm`, `grep` e heredoc `<<'EOF'` — **não** usar sintaxe PowerShell.
- **Cobertura de linha ≥ 80%** no CI (gate). DTO/Request/Response já são excluídos por `coverlet.runsettings`.
- **Branch:** `fase-2`. Commits em pt-BR terminando com:
  `Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>`

## Estrutura de arquivos (o que este plano cria/modifica/deleta)

```
# ---- Task 1 (Parte A): remover Domain Events mortos ----
src/Oficina.Dominio/
  OrdensServico/OrdemDeServico.cs                            MODIFICADO (remove _eventos, EventosNaoPublicados, LimparEventos, Adds)
  OrdensServico/ExcecoesOrdemServico.cs                      MODIFICADO (remove records EventoOs e derivados; mantém exceções)
src/Oficina.Infraestrutura/
  Persistencia/Configuracoes/OrdemDeServicoConfiguration.cs  MODIFICADO (remove b.Ignore(x => x.EventosNaoPublicados))
src/Oficina.Aplicacao/
  OrdensServico/CriarOrdemUseCase.cs                         MODIFICADO (remove os.LimparEventos())
  OrdensServico/IniciarExecucaoUseCase.cs                    MODIFICADO (remove ordem.LimparEventos())
tests/Oficina.Dominio.Testes/
  OrdensServico/OrdemDeServicoTestes.cs                      MODIFICADO (remove assert de evento; mantém transição)

# ---- Task 2 (Parte B): refactor estrutural ----
src/Oficina.Aplicacao/
  OrdensServico/Gateways/IOrdemDeServicoGateway.cs           ★NOVO (abstração + record MetricaTempoMedio movido p/ cá)
  OrdensServico/OrdemInvalidaException.cs                    ★NOVO (movida de dentro de CriarOrdemUseCase.cs)
  OrdensServico/CriarOrdemUseCase.cs                         MODIFICADO (gateway; retorna OrdemDeServico; remove classe de exceção)
  OrdensServico/ObterOrdemPorIdUseCase.cs                    MODIFICADO (gateway; retorna OrdemDeServico?)
  OrdensServico/ListarOrdensUseCase.cs                       MODIFICADO (gateway; retorna ResultadoListaOrdens; mantém PaginaOrdens)
  OrdensServico/IniciarDiagnosticoUseCase.cs                 MODIFICADO (gateway; retorna OrdemDeServico?)
  OrdensServico/EnviarOrcamentoParaAprovacaoUseCase.cs       MODIFICADO (gateway; retorna OrdemDeServico?)
  OrdensServico/IniciarExecucaoUseCase.cs                    MODIFICADO (gateway; retorna OrdemDeServico?; preserva IPecaGateway + transação)
  OrdensServico/FinalizarOrdemUseCase.cs                     MODIFICADO (gateway; retorna OrdemDeServico?)
  OrdensServico/EntregarOrdemUseCase.cs                      MODIFICADO (gateway; retorna OrdemDeServico?)
  OrdensServico/AdicionarItemServicoUseCase.cs               MODIFICADO (gateway; retorna ItemServico?; preserva IServicoGateway)
  OrdensServico/RemoverItemServicoUseCase.cs                 MODIFICADO (gateway; retorna bool)
  OrdensServico/AdicionarItemPecaUseCase.cs                  MODIFICADO (gateway; retorna ItemPeca?; preserva IPecaGateway)
  OrdensServico/RemoverItemPecaUseCase.cs                    MODIFICADO (gateway; retorna bool)
  OrdensServico/ObterTempoMedioExecucaoUseCase.cs            MODIFICADO (gateway; retorna MetricaTempoMedio)
  OrdensServico/MapeadorOrdem.cs                             DELETADO (vira OrdemDeServicoPresenter)
  Consulta/ConsultarOrdemPorNumeroUseCase.cs                 MODIFICADO (IOrdemDeServicoRepositorio → IOrdemDeServicoGateway)
  Consulta/AprovarOrcamentoPorClienteUseCase.cs              MODIFICADO (idem)
  Consulta/RejeitarOrcamentoPorClienteUseCase.cs             MODIFICADO (idem)
src/Oficina.Adaptadores/
  OrdensServico/DataSources/IOrdemDeServicoDataSource.cs     ★NOVO (interface consumida pelo Gateway)
  OrdensServico/Gateways/OrdemDeServicoGateway.cs            ★NOVO (IOrdemDeServicoGateway → delega p/ IOrdemDeServicoDataSource)
  OrdensServico/Presenters/OrdemDeServicoPresenter.cs        ★NOVO (OrdemDeServico/itens/métrica → Response / PaginaOrdens)
  OrdensServico/Controllers/OrdemDeServicoController.cs      ★NOVO (orquestra os 13 use cases + Presenter)
  DependencyInjectionAdaptadores.cs                          MODIFICADO (+ IOrdemDeServicoGateway e OrdemDeServicoController)
src/Oficina.Dominio/
  OrdensServico/IOrdemDeServicoRepositorio.cs                DELETADO (vira IOrdemDeServicoGateway; leva o record MetricaTempoMedio)
src/Oficina.Infraestrutura/
  Persistencia/DataSources/OrdemDeServicoDataSource.cs       ★NOVO (EF Core; corpo do antigo repositório)
  Persistencia/Repositorios/OrdemDeServicoRepositorio.cs     DELETADO
  DependencyInjectionRepositorios.cs                         MODIFICADO (troca binding Ordens)
src/Oficina.Api/
  Controllers/OrdensServicoController.cs                     MODIFICADO (fino; delega ao OrdemDeServicoController)
tests/Oficina.Adaptadores.Testes/
  OrdensServico/OrdemDeServicoPresenterTestes.cs             ★NOVO
  OrdensServico/OrdemDeServicoGatewayTestes.cs               ★NOVO (delegação, incl. EmTransacaoSerializadaAsync e ObterTempoMedioExecucaoAsync)
  OrdensServico/OrdemDeServicoControllerTestes.cs            ★NOVO
tests/Oficina.Aplicacao.Testes/
  OrdensServico/CriarOrdemUseCaseTestes.cs                   MODIFICADO (mock IOrdemDeServicoGateway; assert entidade)
  OrdensServico/IniciarExecucaoUseCaseTestes.cs              MODIFICADO (mock IOrdemDeServicoGateway; retorno vira OrdemDeServico?)
  OrdensServico/AdicionarItensUseCaseTestes.cs               MODIFICADO (mock IOrdemDeServicoGateway; asserts em propriedades da entidade)
  Consulta/ConsultarOrdemPorNumeroUseCaseTestes.cs           MODIFICADO (Mock<IOrdemDeServicoRepositorio> → Mock<IOrdemDeServicoGateway>)
  Consulta/AprovarOrcamentoPorClienteUseCaseTestes.cs        MODIFICADO (idem)
```

**Resumo:** 2 tasks. Task 1 modifica **6** arquivos. Task 2 cria **10**, modifica **24** e deleta **3** (`CriarOrdemUseCase.cs` e `IniciarExecucaoUseCase.cs` aparecem nas duas tasks). Contrato HTTP inalterado (validado no CI pelos testes de integração).

---

## Task 1: Remover os Domain Events mortos (cleanup de domínio)

Os eventos de `OrdemDeServico` (`OrdemDeServicoCriadaEvent`, `ExecucaoIniciadaEvent`, `OrdemFinalizadaEvent`, `OrdemEntregueEvent`) são **código morto**: nunca são despachados por nenhum handler; apenas criados nos métodos de transição e limpos com `LimparEventos()` nos use cases. Esta task os remove por completo, mantendo intactas as transições de estado e as exceções de domínio. Ao final: **build verde + testes de Domínio verdes** (a suíte de Aplicação também continua compilando e passando, pois só perdem a chamada `LimparEventos()`).

**Files:**
- Modify: `src/Oficina.Dominio/OrdensServico/OrdemDeServico.cs`
- Modify: `src/Oficina.Dominio/OrdensServico/ExcecoesOrdemServico.cs`
- Modify: `src/Oficina.Infraestrutura/Persistencia/Configuracoes/OrdemDeServicoConfiguration.cs`
- Modify: `src/Oficina.Aplicacao/OrdensServico/CriarOrdemUseCase.cs`
- Modify: `src/Oficina.Aplicacao/OrdensServico/IniciarExecucaoUseCase.cs`
- Modify: `tests/Oficina.Dominio.Testes/OrdensServico/OrdemDeServicoTestes.cs`

**Interfaces:**
- Produces: `OrdemDeServico` sem `EventosNaoPublicados`/`LimparEventos()`/`_eventos`. As assinaturas públicas de transição (`Criar`, `IniciarDiagnostico`, `EnviarOrcamentoParaAprovacao`, `Aprovar`, `Rejeitar`, `IniciarExecucao`, `Finalizar`, `Entregar`, `Adicionar*`, `Remover*`) **não mudam**. `ExcecoesOrdemServico.cs` mantém todas as exceções e perde os records de evento.

---

- [ ] **Step 1: Remover os eventos de `OrdemDeServico.cs` (arquivo completo)**

Substituir `src/Oficina.Dominio/OrdensServico/OrdemDeServico.cs` inteiro por:
```csharp
namespace Oficina.Dominio.OrdensServico;

public sealed class OrdemDeServico
{
    private readonly List<ItemServico> _itensServico = new();
    private readonly List<ItemPeca> _itensPeca = new();

    public Guid Id { get; private set; }
    public long Numero { get; private set; }            // BIGSERIAL — preenchido pelo banco
    public Guid ClienteId { get; private set; }
    public Guid VeiculoId { get; private set; }
    public StatusOrdemDeServico Status { get; private set; }
    public string? Observacoes { get; private set; }

    public DateTimeOffset CriadaEm { get; private set; }
    public DateTimeOffset? DiagnosticadaEm { get; private set; }
    public DateTimeOffset? EnviadaAprovacaoEm { get; private set; }
    public DateTimeOffset? OrcamentoAprovadoEm { get; private set; }
    public DateTimeOffset? OrcamentoRejeitadoEm { get; private set; }
    public DateTimeOffset? IniciadaEm { get; private set; }
    public DateTimeOffset? FinalizadaEm { get; private set; }
    public DateTimeOffset? EntregueEm { get; private set; }

    public IReadOnlyCollection<ItemServico> ItensServico => _itensServico.AsReadOnly();
    public IReadOnlyCollection<ItemPeca> ItensPeca => _itensPeca.AsReadOnly();

    public decimal TotalServicos => _itensServico.Sum(i => i.Subtotal);
    public decimal TotalPecas => _itensPeca.Sum(i => i.Subtotal);
    public decimal TotalGeral => TotalServicos + TotalPecas;
    public TimeSpan? DuracaoExecucao =>
        IniciadaEm.HasValue && FinalizadaEm.HasValue
            ? FinalizadaEm.Value - IniciadaEm.Value
            : null;

    private OrdemDeServico() { }

    public static OrdemDeServico Criar(Guid clienteId, Guid veiculoId, string? observacoes = null)
    {
        if (clienteId == Guid.Empty)
            throw new ArgumentException("ClienteId obrigatório.", nameof(clienteId));
        if (veiculoId == Guid.Empty)
            throw new ArgumentException("VeiculoId obrigatório.", nameof(veiculoId));

        var os = new OrdemDeServico
        {
            Id = Guid.NewGuid(),
            ClienteId = clienteId,
            VeiculoId = veiculoId,
            Status = StatusOrdemDeServico.Recebida,
            CriadaEm = DateTimeOffset.UtcNow,
            Observacoes = observacoes?.Trim()
        };
        return os;
    }

    public void IniciarDiagnostico()
    {
        if (Status != StatusOrdemDeServico.Recebida)
            throw new TransicaoDeStatusInvalidaException(Status, "iniciar diagnóstico");
        Status = StatusOrdemDeServico.EmDiagnostico;
        DiagnosticadaEm = DateTimeOffset.UtcNow;
    }

    public ItemServico AdicionarItemServico(Guid servicoId, string servicoNome, decimal precoSnapshot, int quantidade)
    {
        GarantirEditavel();
        var item = ItemServico.Criar(servicoId, servicoNome, precoSnapshot, quantidade);
        _itensServico.Add(item);
        return item;
    }

    public void RemoverItemServico(Guid itemId)
    {
        GarantirEditavel();
        var item = _itensServico.FirstOrDefault(i => i.Id == itemId)
            ?? throw new ItemNaoEncontradoException("serviço", itemId);
        _itensServico.Remove(item);
    }

    public ItemPeca AdicionarItemPeca(Guid pecaId, string pecaNome, decimal precoSnapshot, int quantidade)
    {
        GarantirEditavel();
        var item = ItemPeca.Criar(pecaId, pecaNome, precoSnapshot, quantidade);
        _itensPeca.Add(item);
        return item;
    }

    public void RemoverItemPeca(Guid itemId)
    {
        GarantirEditavel();
        var item = _itensPeca.FirstOrDefault(i => i.Id == itemId)
            ?? throw new ItemNaoEncontradoException("peça", itemId);
        _itensPeca.Remove(item);
    }

    public void EnviarOrcamentoParaAprovacao()
    {
        if (Status != StatusOrdemDeServico.EmDiagnostico)
            throw new TransicaoDeStatusInvalidaException(Status, "enviar para aprovação");
        if (_itensServico.Count == 0 && _itensPeca.Count == 0)
            throw new OrdemSemItensException();
        Status = StatusOrdemDeServico.AguardandoAprovacao;
        EnviadaAprovacaoEm = DateTimeOffset.UtcNow;
    }

    public void Aprovar()
    {
        if (Status != StatusOrdemDeServico.AguardandoAprovacao)
            throw new TransicaoDeStatusInvalidaException(Status, "aprovar orçamento");
        OrcamentoAprovadoEm = DateTimeOffset.UtcNow;
    }

    public void Rejeitar()
    {
        if (Status != StatusOrdemDeServico.AguardandoAprovacao)
            throw new TransicaoDeStatusInvalidaException(Status, "rejeitar orçamento");
        OrcamentoRejeitadoEm = DateTimeOffset.UtcNow;
        Status = StatusOrdemDeServico.Cancelada;
    }

    public void IniciarExecucao()
    {
        if (Status != StatusOrdemDeServico.AguardandoAprovacao)
            throw new TransicaoDeStatusInvalidaException(Status, "iniciar execução");
        if (OrcamentoAprovadoEm is null)
            throw new OrcamentoNaoAprovadoException();

        Status = StatusOrdemDeServico.EmExecucao;
        IniciadaEm = DateTimeOffset.UtcNow;
    }

    public void Finalizar()
    {
        if (Status != StatusOrdemDeServico.EmExecucao)
            throw new TransicaoDeStatusInvalidaException(Status, "finalizar");
        Status = StatusOrdemDeServico.Finalizada;
        FinalizadaEm = DateTimeOffset.UtcNow;
    }

    public void Entregar()
    {
        if (Status != StatusOrdemDeServico.Finalizada)
            throw new TransicaoDeStatusInvalidaException(Status, "entregar");
        Status = StatusOrdemDeServico.Entregue;
        EntregueEm = DateTimeOffset.UtcNow;
    }

    private void GarantirEditavel()
    {
        if (Status is StatusOrdemDeServico.EmExecucao
                   or StatusOrdemDeServico.Finalizada
                   or StatusOrdemDeServico.Entregue
                   or StatusOrdemDeServico.Cancelada)
            throw new OrdemImutavelException();
    }
}
```

- [ ] **Step 2: Remover os records de evento de `ExcecoesOrdemServico.cs` (arquivo completo)**

Substituir `src/Oficina.Dominio/OrdensServico/ExcecoesOrdemServico.cs` inteiro por (mantém todas as exceções; remove os 5 records de evento no fim do arquivo):
```csharp
namespace Oficina.Dominio.OrdensServico;

public class TransicaoDeStatusInvalidaException : Exception
{
    public TransicaoDeStatusInvalidaException(StatusOrdemDeServico atual, string acao)
        : base($"Não é possível {acao} a partir do status '{atual}'.") { }
}

public class OrdemSemItensException : Exception
{
    public OrdemSemItensException()
        : base("A ordem precisa ter ao menos um item antes de enviar para aprovação.") { }
}

public class ItemNaoEncontradoException : Exception
{
    public ItemNaoEncontradoException(string tipo, Guid id)
        : base($"Item de {tipo} com id '{id}' não encontrado nesta ordem.") { }
}

public class OrcamentoNaoAprovadoException : Exception
{
    public OrcamentoNaoAprovadoException()
        : base("Não é possível iniciar execução sem aprovação do orçamento pelo cliente.") { }
}

public class ItemInvalidoException : Exception
{
    public ItemInvalidoException(string mensagem) : base(mensagem) { }
}

public class OrdemImutavelException : Exception
{
    public OrdemImutavelException()
        : base("Itens só podem ser modificados antes do início da execução.") { }
}
```

- [ ] **Step 3: Remover o `Ignore` do evento na configuração EF**

Em `src/Oficina.Infraestrutura/Persistencia/Configuracoes/OrdemDeServicoConfiguration.cs`, remover a linha `b.Ignore(x => x.EventosNaoPublicados);`.

Localizar este bloco:
```csharp
        // Totais e duração — campos calculados, não persistidos
        b.Ignore(x => x.TotalServicos);
        b.Ignore(x => x.TotalPecas);
        b.Ignore(x => x.TotalGeral);
        b.Ignore(x => x.DuracaoExecucao);
        b.Ignore(x => x.EventosNaoPublicados);
```
E substituir por (sem a última linha):
```csharp
        // Totais e duração — campos calculados, não persistidos
        b.Ignore(x => x.TotalServicos);
        b.Ignore(x => x.TotalPecas);
        b.Ignore(x => x.TotalGeral);
        b.Ignore(x => x.DuracaoExecucao);
```

- [ ] **Step 4: Remover `os.LimparEventos()` de `CriarOrdemUseCase.cs`**

Em `src/Oficina.Aplicacao/OrdensServico/CriarOrdemUseCase.cs`, localizar:
```csharp
        var os = OrdemDeServico.Criar(cliente.Id, veiculo.Id, req.Observacoes);
        await _repo.AdicionarAsync(os, ct);
        await _repo.SalvarAsync(ct);
        os.LimparEventos();

        // Recarrega para popular Numero (BIGSERIAL preenchido pelo banco)
```
E substituir por:
```csharp
        var os = OrdemDeServico.Criar(cliente.Id, veiculo.Id, req.Observacoes);
        await _repo.AdicionarAsync(os, ct);
        await _repo.SalvarAsync(ct);

        // Recarrega para popular Numero (BIGSERIAL preenchido pelo banco)
```

- [ ] **Step 5: Remover `ordem.LimparEventos()` de `IniciarExecucaoUseCase.cs`**

Em `src/Oficina.Aplicacao/OrdensServico/IniciarExecucaoUseCase.cs`, localizar:
```csharp
            // 3) persiste tudo na mesma transação
            await _ordens.SalvarAsync(tx);
            ordem.LimparEventos();

            resposta = MapeadorOrdem.Mapear(ordem);
```
E substituir por:
```csharp
            // 3) persiste tudo na mesma transação
            await _ordens.SalvarAsync(tx);

            resposta = MapeadorOrdem.Mapear(ordem);
```

- [ ] **Step 6: Ajustar o teste de Domínio (remover o assert de evento, manter a transição)**

Em `tests/Oficina.Dominio.Testes/OrdensServico/OrdemDeServicoTestes.cs`, localizar o teste:
```csharp
    [Fact]
    public void IniciarExecucao_AposAprovacao_DeveAvancarEDispararEvento()
    {
        var os = ComItens();
        os.IniciarDiagnostico();
        os.EnviarOrcamentoParaAprovacao();
        os.Aprovar();

        os.IniciarExecucao();

        os.Status.Should().Be(StatusOrdemDeServico.EmExecucao);
        os.IniciadaEm.Should().NotBeNull();
        os.EventosNaoPublicados.Should().Contain(e => e is ExecucaoIniciadaEvent);
    }
```
E substituir por (remove a última asserção e o "EDispararEvento" do nome; a transição continua coberta):
```csharp
    [Fact]
    public void IniciarExecucao_AposAprovacao_DeveAvancar()
    {
        var os = ComItens();
        os.IniciarDiagnostico();
        os.EnviarOrcamentoParaAprovacao();
        os.Aprovar();

        os.IniciarExecucao();

        os.Status.Should().Be(StatusOrdemDeServico.EmExecucao);
        os.IniciadaEm.Should().NotBeNull();
    }
```

- [ ] **Step 7: Verificar que não sobrou nenhuma referência aos eventos**

Run:
```bash
cd "C:/Users/thiago/source/repos/Fiap/fiap_15SOAT_fase1"
grep -rn "EventoOs\|EventosNaoPublicados\|LimparEventos\|CriadaEvent\|IniciadaEvent\|FinalizadaEvent\|EntregueEvent" src tests --include=*.cs
```
Expected: **nenhuma linha** de saída (o padrão `IniciadaEvent` casa com `ExecucaoIniciadaEvent`, `CriadaEvent` com `OrdemDeServicoCriadaEvent`, etc.). Se algo aparecer, remover a referência restante antes de prosseguir.

- [ ] **Step 8: Compilar a solution**

Run: `dotnet build Oficina.sln`
Expected: **Build succeeded**, 0 erros.

- [ ] **Step 9: Rodar os testes de Domínio e de Aplicação (um projeto por chamada)**

Run:
```bash
dotnet test tests/Oficina.Dominio.Testes
dotnet test tests/Oficina.Aplicacao.Testes
```
Expected: PASS em ambos. `OrdemDeServicoTestes` (Domínio) valida as transições sem os eventos; `Oficina.Aplicacao.Testes` continua verde (só perderam as chamadas `LimparEventos()`).

- [ ] **Step 10: Commit**

```bash
cd "C:/Users/thiago/source/repos/Fiap/fiap_15SOAT_fase1"
git add -A
git commit -m "$(cat <<'EOF'
refactor: remover Domain Events mortos de OrdemDeServico

Os eventos (OrdemDeServicoCriadaEvent, ExecucaoIniciadaEvent, OrdemFinalizadaEvent,
OrdemEntregueEvent) nunca eram despachados — apenas criados nas transicoes e limpos
via LimparEventos() nos use cases. Remove o campo _eventos, a propriedade
EventosNaoPublicados, o metodo LimparEventos(), os Add nos metodos de transicao, os
records de evento em ExcecoesOrdemServico.cs, o b.Ignore(EventosNaoPublicados) na
configuracao EF e as chamadas LimparEventos() em CriarOrdemUseCase e
IniciarExecucaoUseCase. As transicoes de estado e as excecoes de dominio ficam
intactas; o teste de dominio deixa de assertar o evento (mantendo a transicao).

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 2: Refatorar o contexto Ordens de Serviço para Clean Architecture

Refatora Ordens de Serviço ponta-a-ponta. Muitos passos pequenos; o build só fica verde ao final (a deleção de `IOrdemDeServicoRepositorio` quebra tudo até que casos de uso, cross-context Consulta, DataSource e DI estejam prontos). Ao final: **build verde + os 3 projetos de teste unitários verdes**, com o contrato HTTP intacto (validado no CI pelos testes de integração).

**Files:**
- Create: `src/Oficina.Aplicacao/OrdensServico/Gateways/IOrdemDeServicoGateway.cs`
- Create: `src/Oficina.Aplicacao/OrdensServico/OrdemInvalidaException.cs`
- Create: `src/Oficina.Adaptadores/OrdensServico/DataSources/IOrdemDeServicoDataSource.cs`
- Create: `src/Oficina.Adaptadores/OrdensServico/Gateways/OrdemDeServicoGateway.cs`
- Create: `src/Oficina.Adaptadores/OrdensServico/Presenters/OrdemDeServicoPresenter.cs`
- Create: `src/Oficina.Adaptadores/OrdensServico/Controllers/OrdemDeServicoController.cs`
- Create: `src/Oficina.Infraestrutura/Persistencia/DataSources/OrdemDeServicoDataSource.cs`
- Create: `tests/Oficina.Adaptadores.Testes/OrdensServico/OrdemDeServicoPresenterTestes.cs`
- Create: `tests/Oficina.Adaptadores.Testes/OrdensServico/OrdemDeServicoGatewayTestes.cs`
- Create: `tests/Oficina.Adaptadores.Testes/OrdensServico/OrdemDeServicoControllerTestes.cs`
- Modify: os 13 use cases de `src/Oficina.Aplicacao/OrdensServico/`
- Modify: `src/Oficina.Aplicacao/Consulta/{ConsultarOrdemPorNumero,AprovarOrcamentoPorCliente,RejeitarOrcamentoPorCliente}UseCase.cs`
- Modify: `src/Oficina.Api/Controllers/OrdensServicoController.cs`
- Modify: `src/Oficina.Infraestrutura/DependencyInjectionRepositorios.cs`
- Modify: `src/Oficina.Adaptadores/DependencyInjectionAdaptadores.cs`
- Modify: `tests/Oficina.Aplicacao.Testes/OrdensServico/{CriarOrdem,IniciarExecucao,AdicionarItens}UseCaseTestes.cs`
- Modify: `tests/Oficina.Aplicacao.Testes/Consulta/{ConsultarOrdemPorNumero,AprovarOrcamentoPorCliente}UseCaseTestes.cs`
- Delete: `src/Oficina.Dominio/OrdensServico/IOrdemDeServicoRepositorio.cs`, `src/Oficina.Infraestrutura/Persistencia/Repositorios/OrdemDeServicoRepositorio.cs`, `src/Oficina.Aplicacao/OrdensServico/MapeadorOrdem.cs`

**Interfaces (padrão idêntico ao Estoque):**
- `Oficina.Aplicacao.OrdensServico.Gateways.IOrdemDeServicoGateway` — mesma forma do antigo `IOrdemDeServicoRepositorio` (inclui `EmTransacaoSerializadaAsync`, `ObterTempoMedioExecucaoAsync`, `ObterPorNumeroAsync`, `MarcarItemServicoComoNovo`, `MarcarItemPecaComoNovo`). O record `MetricaTempoMedio` mora **neste** arquivo/namespace.
- `Oficina.Adaptadores.OrdensServico.DataSources.IOrdemDeServicoDataSource` — mesma forma.
- `Oficina.Adaptadores.OrdensServico.Gateways.OrdemDeServicoGateway : IOrdemDeServicoGateway`.
- `Oficina.Adaptadores.OrdensServico.Presenters.OrdemDeServicoPresenter` (estático): `Apresentar(OrdemDeServico) : OrdemResponse`, `ApresentarItemServico(ItemServico) : ItemServicoResponse`, `ApresentarItemPeca(ItemPeca) : ItemPecaResponse`, `ApresentarPagina(IReadOnlyList<OrdemDeServico>, int, int, int) : PaginaOrdens`, `ApresentarMetrica(MetricaTempoMedio) : MetricasTempoMedioResponse`.
- `Oficina.Adaptadores.OrdensServico.Controllers.OrdemDeServicoController` — `CriarAsync`, `ObterPorIdAsync`, `ListarAsync`, `IniciarDiagnosticoAsync`, `EnviarOrcamentoParaAprovacaoAsync`, `IniciarExecucaoAsync`, `FinalizarAsync`, `EntregarAsync`, `AdicionarItemServicoAsync`, `RemoverItemServicoAsync`, `AdicionarItemPecaAsync`, `RemoverItemPecaAsync`, `ObterTempoMedioExecucaoAsync`.
- Use cases retornam entidade: `CriarOrdemUseCase → Task<OrdemDeServico>`, `ObterOrdemPorIdUseCase → Task<OrdemDeServico?>`, `ListarOrdensUseCase → Task<ResultadoListaOrdens>`, `IniciarDiagnosticoUseCase/EnviarOrcamentoParaAprovacaoUseCase/IniciarExecucaoUseCase/FinalizarOrdemUseCase/EntregarOrdemUseCase → Task<OrdemDeServico?>`, `AdicionarItemServicoUseCase → Task<ItemServico?>`, `AdicionarItemPecaUseCase → Task<ItemPeca?>`, `RemoverItemServicoUseCase/RemoverItemPecaUseCase → Task<bool>`, `ObterTempoMedioExecucaoUseCase → Task<MetricaTempoMedio>`.
- `Oficina.Aplicacao.OrdensServico.ResultadoListaOrdens(IReadOnlyList<OrdemDeServico> Itens, int Total, int Pagina, int TamanhoPagina)`.
- `Oficina.Aplicacao.OrdensServico.PaginaOrdens(IReadOnlyList<OrdemResponse> Itens, int Total, int Pagina, int TamanhoPagina)` — **mantida no namespace `Oficina.Aplicacao.OrdensServico`** (contrato inalterado).
- `Oficina.Aplicacao.OrdensServico.OrdemInvalidaException` — movida de dentro de `CriarOrdemUseCase.cs` para arquivo próprio (mesmo namespace; consumidores não mudam de `using`).

---

- [ ] **Step 1: Criar `IOrdemDeServicoGateway` (camada de Casos de Uso) — com o record `MetricaTempoMedio`**

Create `src/Oficina.Aplicacao/OrdensServico/Gateways/IOrdemDeServicoGateway.cs`:
```csharp
using Oficina.Dominio.OrdensServico;

namespace Oficina.Aplicacao.OrdensServico.Gateways;

public interface IOrdemDeServicoGateway
{
    Task<OrdemDeServico?> ObterPorIdAsync(Guid id, CancellationToken ct);
    Task<OrdemDeServico?> ObterPorNumeroAsync(long numero, CancellationToken ct);
    Task<IReadOnlyList<OrdemDeServico>> ListarAsync(StatusOrdemDeServico? statusFiltro, int pagina, int tamanhoPagina, CancellationToken ct);
    Task<int> ContarAsync(StatusOrdemDeServico? statusFiltro, CancellationToken ct);
    Task AdicionarAsync(OrdemDeServico ordem, CancellationToken ct);
    Task SalvarAsync(CancellationToken ct);

    /// <summary>
    /// Executa uma operação em transação serializável (baixa de estoque + mudança
    /// de estado da OS na mesma transação).
    /// </summary>
    Task EmTransacaoSerializadaAsync(Func<CancellationToken, Task> acao, CancellationToken ct);

    Task<MetricaTempoMedio> ObterTempoMedioExecucaoAsync(CancellationToken ct);

    // Forca o estado Added para itens novos adicionados via navigation
    // collection (workaround para bug de change detection com Id pre-setado).
    void MarcarItemServicoComoNovo(ItemServico item);
    void MarcarItemPecaComoNovo(ItemPeca item);
}

// Novo lar do record (antes vivia em Oficina.Dominio.OrdensServico/IOrdemDeServicoRepositorio.cs).
public sealed record MetricaTempoMedio(
    int TotalOrdensConcluidas,
    TimeSpan? TempoMedio,
    TimeSpan? TempoMinimo,
    TimeSpan? TempoMaximo);
```

- [ ] **Step 2: Extrair `OrdemInvalidaException` para arquivo próprio**

Create `src/Oficina.Aplicacao/OrdensServico/OrdemInvalidaException.cs`:
```csharp
namespace Oficina.Aplicacao.OrdensServico;

public class OrdemInvalidaException : Exception
{
    public OrdemInvalidaException(string mensagem) : base(mensagem) { }
}
```

> A classe será removida de `CriarOrdemUseCase.cs` no Step 3. Como fica no mesmo namespace (`Oficina.Aplicacao.OrdensServico`), nenhum consumidor (`ListarOrdensUseCase`, `AdicionarItemServicoUseCase`, `AdicionarItemPecaUseCase`, `IniciarExecucaoUseCase`, testes) precisa de `using` novo.

- [ ] **Step 3: Refatorar os 13 casos de uso de Ordens de Serviço (depender do gateway; retornar entidades)**

Substituir `src/Oficina.Aplicacao/OrdensServico/CriarOrdemUseCase.cs` (remove a classe de exceção — agora em arquivo próprio; consome `IOrdemDeServicoGateway`; retorna a entidade recarregada com `Numero`):
```csharp
using Oficina.Aplicacao.Clientes.Gateways;
using Oficina.Aplicacao.OrdensServico.Dtos;
using Oficina.Aplicacao.OrdensServico.Gateways;
using Oficina.Dominio.OrdensServico;

namespace Oficina.Aplicacao.OrdensServico;

public class CriarOrdemUseCase
{
    private readonly IOrdemDeServicoGateway _gateway;
    private readonly IClienteGateway _clientes;

    public CriarOrdemUseCase(IOrdemDeServicoGateway gateway, IClienteGateway clientes)
    {
        _gateway = gateway;
        _clientes = clientes;
    }

    public async Task<OrdemDeServico> ExecutarAsync(CriarOrdemRequest req, CancellationToken ct)
    {
        var cliente = await _clientes.ObterPorIdAsync(req.ClienteId, ct)
            ?? throw new OrdemInvalidaException("Cliente não encontrado.");

        if (!cliente.Ativo)
            throw new OrdemInvalidaException("Cliente inativo.");

        var veiculo = cliente.Veiculos.FirstOrDefault(v => v.Id == req.VeiculoId)
            ?? throw new OrdemInvalidaException("Veículo não pertence ao cliente informado.");

        var os = OrdemDeServico.Criar(cliente.Id, veiculo.Id, req.Observacoes);
        await _gateway.AdicionarAsync(os, ct);
        await _gateway.SalvarAsync(ct);

        // Recarrega para popular Numero (BIGSERIAL preenchido pelo banco)
        return await _gateway.ObterPorIdAsync(os.Id, ct)
            ?? throw new InvalidOperationException("OS não encontrada após criação.");
    }
}
```

Substituir `src/Oficina.Aplicacao/OrdensServico/ObterOrdemPorIdUseCase.cs`:
```csharp
using Oficina.Aplicacao.OrdensServico.Gateways;
using Oficina.Dominio.OrdensServico;

namespace Oficina.Aplicacao.OrdensServico;

public class ObterOrdemPorIdUseCase
{
    private readonly IOrdemDeServicoGateway _gateway;
    public ObterOrdemPorIdUseCase(IOrdemDeServicoGateway gateway) => _gateway = gateway;

    public Task<OrdemDeServico?> ExecutarAsync(Guid id, CancellationToken ct) =>
        _gateway.ObterPorIdAsync(id, ct);
}
```

Substituir `src/Oficina.Aplicacao/OrdensServico/ListarOrdensUseCase.cs` (mantém `PaginaOrdens` no namespace; adiciona `ResultadoListaOrdens`; preserva o parse de status e o `OrdemInvalidaException` para status inválido):
```csharp
using Oficina.Aplicacao.OrdensServico.Dtos;
using Oficina.Aplicacao.OrdensServico.Gateways;
using Oficina.Dominio.OrdensServico;

namespace Oficina.Aplicacao.OrdensServico;

// Response DTO consumido pelo Presenter e pelo cliente HTTP (mantido neste namespace por compatibilidade).
public sealed record PaginaOrdens(IReadOnlyList<OrdemResponse> Itens, int Total, int Pagina, int TamanhoPagina);

// Resultado do use case em termos de entidades de domínio (o Presenter converte em PaginaOrdens).
public sealed record ResultadoListaOrdens(IReadOnlyList<OrdemDeServico> Itens, int Total, int Pagina, int TamanhoPagina);

public class ListarOrdensUseCase
{
    private readonly IOrdemDeServicoGateway _gateway;
    public ListarOrdensUseCase(IOrdemDeServicoGateway gateway) => _gateway = gateway;

    public async Task<ResultadoListaOrdens> ExecutarAsync(string? statusFiltro, int pagina, int tamanho, CancellationToken ct)
    {
        StatusOrdemDeServico? status = null;
        if (!string.IsNullOrWhiteSpace(statusFiltro))
        {
            if (!Enum.TryParse<StatusOrdemDeServico>(statusFiltro, true, out var s))
                throw new OrdemInvalidaException($"Status '{statusFiltro}' inválido.");
            status = s;
        }

        var lista = await _gateway.ListarAsync(status, pagina, tamanho, ct);
        var total = await _gateway.ContarAsync(status, ct);
        return new ResultadoListaOrdens(lista, total, pagina, tamanho);
    }
}
```

Substituir `src/Oficina.Aplicacao/OrdensServico/IniciarDiagnosticoUseCase.cs`:
```csharp
using Oficina.Aplicacao.OrdensServico.Gateways;
using Oficina.Dominio.OrdensServico;

namespace Oficina.Aplicacao.OrdensServico;

public class IniciarDiagnosticoUseCase
{
    private readonly IOrdemDeServicoGateway _gateway;
    public IniciarDiagnosticoUseCase(IOrdemDeServicoGateway gateway) => _gateway = gateway;

    public async Task<OrdemDeServico?> ExecutarAsync(Guid id, CancellationToken ct)
    {
        var os = await _gateway.ObterPorIdAsync(id, ct);
        if (os is null) return null;
        os.IniciarDiagnostico();
        await _gateway.SalvarAsync(ct);
        return os;
    }
}
```

Substituir `src/Oficina.Aplicacao/OrdensServico/EnviarOrcamentoParaAprovacaoUseCase.cs`:
```csharp
using Oficina.Aplicacao.OrdensServico.Gateways;
using Oficina.Dominio.OrdensServico;

namespace Oficina.Aplicacao.OrdensServico;

public class EnviarOrcamentoParaAprovacaoUseCase
{
    private readonly IOrdemDeServicoGateway _gateway;
    public EnviarOrcamentoParaAprovacaoUseCase(IOrdemDeServicoGateway gateway) => _gateway = gateway;

    public async Task<OrdemDeServico?> ExecutarAsync(Guid id, CancellationToken ct)
    {
        var os = await _gateway.ObterPorIdAsync(id, ct);
        if (os is null) return null;
        os.EnviarOrcamentoParaAprovacao();
        await _gateway.SalvarAsync(ct);
        return os;
    }
}
```

Substituir `src/Oficina.Aplicacao/OrdensServico/IniciarExecucaoUseCase.cs` (preserva `IPecaGateway` e a transação serializável de **ordens**; retorna a entidade; `LimparEventos()` já removido na Task 1):
```csharp
using Oficina.Aplicacao.Estoque.Gateways;
using Oficina.Aplicacao.OrdensServico.Gateways;
using Oficina.Dominio.OrdensServico;

namespace Oficina.Aplicacao.OrdensServico;

public class IniciarExecucaoUseCase
{
    private readonly IOrdemDeServicoGateway _ordens;
    private readonly IPecaGateway _pecas;

    public IniciarExecucaoUseCase(IOrdemDeServicoGateway ordens, IPecaGateway pecas)
    {
        _ordens = ordens;
        _pecas = pecas;
    }

    public async Task<OrdemDeServico?> ExecutarAsync(Guid ordemId, CancellationToken ct)
    {
        OrdemDeServico? resultado = null;

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

            resultado = ordem;
        }, ct);

        return resultado;
    }
}
```

Substituir `src/Oficina.Aplicacao/OrdensServico/FinalizarOrdemUseCase.cs`:
```csharp
using Oficina.Aplicacao.OrdensServico.Gateways;
using Oficina.Dominio.OrdensServico;

namespace Oficina.Aplicacao.OrdensServico;

public class FinalizarOrdemUseCase
{
    private readonly IOrdemDeServicoGateway _gateway;
    public FinalizarOrdemUseCase(IOrdemDeServicoGateway gateway) => _gateway = gateway;

    public async Task<OrdemDeServico?> ExecutarAsync(Guid id, CancellationToken ct)
    {
        var os = await _gateway.ObterPorIdAsync(id, ct);
        if (os is null) return null;
        os.Finalizar();
        await _gateway.SalvarAsync(ct);
        return os;
    }
}
```

Substituir `src/Oficina.Aplicacao/OrdensServico/EntregarOrdemUseCase.cs`:
```csharp
using Oficina.Aplicacao.OrdensServico.Gateways;
using Oficina.Dominio.OrdensServico;

namespace Oficina.Aplicacao.OrdensServico;

public class EntregarOrdemUseCase
{
    private readonly IOrdemDeServicoGateway _gateway;
    public EntregarOrdemUseCase(IOrdemDeServicoGateway gateway) => _gateway = gateway;

    public async Task<OrdemDeServico?> ExecutarAsync(Guid id, CancellationToken ct)
    {
        var os = await _gateway.ObterPorIdAsync(id, ct);
        if (os is null) return null;
        os.Entregar();
        await _gateway.SalvarAsync(ct);
        return os;
    }
}
```

Substituir `src/Oficina.Aplicacao/OrdensServico/AdicionarItemServicoUseCase.cs` (preserva `IServicoGateway`; retorna a entidade `ItemServico?`):
```csharp
using Oficina.Aplicacao.Catalogo.Gateways;
using Oficina.Aplicacao.OrdensServico.Dtos;
using Oficina.Aplicacao.OrdensServico.Gateways;
using Oficina.Dominio.OrdensServico;

namespace Oficina.Aplicacao.OrdensServico;

public class AdicionarItemServicoUseCase
{
    private readonly IOrdemDeServicoGateway _ordens;
    private readonly IServicoGateway _servicos;

    public AdicionarItemServicoUseCase(IOrdemDeServicoGateway ordens, IServicoGateway servicos)
    {
        _ordens = ordens;
        _servicos = servicos;
    }

    public async Task<ItemServico?> ExecutarAsync(Guid ordemId, AdicionarItemServicoRequest req, CancellationToken ct)
    {
        var ordem = await _ordens.ObterPorIdAsync(ordemId, ct);
        if (ordem is null) return null;

        var servico = await _servicos.ObterPorIdAsync(req.ServicoId, ct)
            ?? throw new OrdemInvalidaException("Serviço não encontrado.");
        if (!servico.Ativo)
            throw new OrdemInvalidaException("Serviço inativo.");

        var item = ordem.AdicionarItemServico(servico.Id, servico.Nome, servico.PrecoBase, req.Quantidade);
        _ordens.MarcarItemServicoComoNovo(item);
        await _ordens.SalvarAsync(ct);

        return item;
    }
}
```

Substituir `src/Oficina.Aplicacao/OrdensServico/RemoverItemServicoUseCase.cs`:
```csharp
using Oficina.Aplicacao.OrdensServico.Gateways;

namespace Oficina.Aplicacao.OrdensServico;

public class RemoverItemServicoUseCase
{
    private readonly IOrdemDeServicoGateway _ordens;
    public RemoverItemServicoUseCase(IOrdemDeServicoGateway ordens) => _ordens = ordens;

    public async Task<bool> ExecutarAsync(Guid ordemId, Guid itemId, CancellationToken ct)
    {
        var ordem = await _ordens.ObterPorIdAsync(ordemId, ct);
        if (ordem is null) return false;
        ordem.RemoverItemServico(itemId);
        await _ordens.SalvarAsync(ct);
        return true;
    }
}
```

Substituir `src/Oficina.Aplicacao/OrdensServico/AdicionarItemPecaUseCase.cs` (preserva `IPecaGateway`; retorna a entidade `ItemPeca?`):
```csharp
using Oficina.Aplicacao.Estoque.Gateways;
using Oficina.Aplicacao.OrdensServico.Dtos;
using Oficina.Aplicacao.OrdensServico.Gateways;
using Oficina.Dominio.OrdensServico;

namespace Oficina.Aplicacao.OrdensServico;

public class AdicionarItemPecaUseCase
{
    private readonly IOrdemDeServicoGateway _ordens;
    private readonly IPecaGateway _pecas;

    public AdicionarItemPecaUseCase(IOrdemDeServicoGateway ordens, IPecaGateway pecas)
    {
        _ordens = ordens;
        _pecas = pecas;
    }

    public async Task<ItemPeca?> ExecutarAsync(Guid ordemId, AdicionarItemPecaRequest req, CancellationToken ct)
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

        return item;
    }
}
```

Substituir `src/Oficina.Aplicacao/OrdensServico/RemoverItemPecaUseCase.cs`:
```csharp
using Oficina.Aplicacao.OrdensServico.Gateways;

namespace Oficina.Aplicacao.OrdensServico;

public class RemoverItemPecaUseCase
{
    private readonly IOrdemDeServicoGateway _ordens;
    public RemoverItemPecaUseCase(IOrdemDeServicoGateway ordens) => _ordens = ordens;

    public async Task<bool> ExecutarAsync(Guid ordemId, Guid itemId, CancellationToken ct)
    {
        var ordem = await _ordens.ObterPorIdAsync(ordemId, ct);
        if (ordem is null) return false;
        ordem.RemoverItemPeca(itemId);
        await _ordens.SalvarAsync(ct);
        return true;
    }
}
```

Substituir `src/Oficina.Aplicacao/OrdensServico/ObterTempoMedioExecucaoUseCase.cs` (a conversão `TimeSpan → double minutos` migra para o Presenter; o use case só delega e retorna a métrica de domínio):
```csharp
using Oficina.Aplicacao.OrdensServico.Gateways;

namespace Oficina.Aplicacao.OrdensServico;

public class ObterTempoMedioExecucaoUseCase
{
    private readonly IOrdemDeServicoGateway _gateway;
    public ObterTempoMedioExecucaoUseCase(IOrdemDeServicoGateway gateway) => _gateway = gateway;

    public Task<MetricaTempoMedio> ExecutarAsync(CancellationToken ct) =>
        _gateway.ObterTempoMedioExecucaoAsync(ct);
}
```

- [ ] **Step 4: Deletar o mapeador da Aplicação (vira Presenter)**

Run:
```bash
cd "C:/Users/thiago/source/repos/Fiap/fiap_15SOAT_fase1"
rm src/Oficina.Aplicacao/OrdensServico/MapeadorOrdem.cs
```

- [ ] **Step 5: Criar `IOrdemDeServicoDataSource` e `OrdemDeServicoGateway` (Adaptadores)**

Create `src/Oficina.Adaptadores/OrdensServico/DataSources/IOrdemDeServicoDataSource.cs`:
```csharp
using Oficina.Aplicacao.OrdensServico.Gateways;
using Oficina.Dominio.OrdensServico;

namespace Oficina.Adaptadores.OrdensServico.DataSources;

public interface IOrdemDeServicoDataSource
{
    Task<OrdemDeServico?> ObterPorIdAsync(Guid id, CancellationToken ct);
    Task<OrdemDeServico?> ObterPorNumeroAsync(long numero, CancellationToken ct);
    Task<IReadOnlyList<OrdemDeServico>> ListarAsync(StatusOrdemDeServico? statusFiltro, int pagina, int tamanhoPagina, CancellationToken ct);
    Task<int> ContarAsync(StatusOrdemDeServico? statusFiltro, CancellationToken ct);
    Task AdicionarAsync(OrdemDeServico ordem, CancellationToken ct);
    Task SalvarAsync(CancellationToken ct);
    Task EmTransacaoSerializadaAsync(Func<CancellationToken, Task> acao, CancellationToken ct);
    Task<MetricaTempoMedio> ObterTempoMedioExecucaoAsync(CancellationToken ct);
    void MarcarItemServicoComoNovo(ItemServico item);
    void MarcarItemPecaComoNovo(ItemPeca item);
}
```

Create `src/Oficina.Adaptadores/OrdensServico/Gateways/OrdemDeServicoGateway.cs`:
```csharp
using Oficina.Adaptadores.OrdensServico.DataSources;
using Oficina.Aplicacao.OrdensServico.Gateways;
using Oficina.Dominio.OrdensServico;

namespace Oficina.Adaptadores.OrdensServico.Gateways;

public class OrdemDeServicoGateway : IOrdemDeServicoGateway
{
    private readonly IOrdemDeServicoDataSource _dataSource;
    public OrdemDeServicoGateway(IOrdemDeServicoDataSource dataSource) => _dataSource = dataSource;

    public Task<OrdemDeServico?> ObterPorIdAsync(Guid id, CancellationToken ct) =>
        _dataSource.ObterPorIdAsync(id, ct);

    public Task<OrdemDeServico?> ObterPorNumeroAsync(long numero, CancellationToken ct) =>
        _dataSource.ObterPorNumeroAsync(numero, ct);

    public Task<IReadOnlyList<OrdemDeServico>> ListarAsync(StatusOrdemDeServico? statusFiltro, int pagina, int tamanhoPagina, CancellationToken ct) =>
        _dataSource.ListarAsync(statusFiltro, pagina, tamanhoPagina, ct);

    public Task<int> ContarAsync(StatusOrdemDeServico? statusFiltro, CancellationToken ct) =>
        _dataSource.ContarAsync(statusFiltro, ct);

    public Task AdicionarAsync(OrdemDeServico ordem, CancellationToken ct) =>
        _dataSource.AdicionarAsync(ordem, ct);

    public Task SalvarAsync(CancellationToken ct) =>
        _dataSource.SalvarAsync(ct);

    public Task EmTransacaoSerializadaAsync(Func<CancellationToken, Task> acao, CancellationToken ct) =>
        _dataSource.EmTransacaoSerializadaAsync(acao, ct);

    public Task<MetricaTempoMedio> ObterTempoMedioExecucaoAsync(CancellationToken ct) =>
        _dataSource.ObterTempoMedioExecucaoAsync(ct);

    public void MarcarItemServicoComoNovo(ItemServico item) =>
        _dataSource.MarcarItemServicoComoNovo(item);

    public void MarcarItemPecaComoNovo(ItemPeca item) =>
        _dataSource.MarcarItemPecaComoNovo(item);
}
```

- [ ] **Step 6: Criar `OrdemDeServicoPresenter` (Adaptadores) — inclui a métrica**

Create `src/Oficina.Adaptadores/OrdensServico/Presenters/OrdemDeServicoPresenter.cs`:
```csharp
using Oficina.Aplicacao.OrdensServico;
using Oficina.Aplicacao.OrdensServico.Dtos;
using Oficina.Aplicacao.OrdensServico.Gateways;
using Oficina.Dominio.OrdensServico;

namespace Oficina.Adaptadores.OrdensServico.Presenters;

public static class OrdemDeServicoPresenter
{
    public static OrdemResponse Apresentar(OrdemDeServico o) => new(
        o.Id, o.Numero, o.ClienteId, o.VeiculoId, o.Status.ToString(), o.Observacoes,
        o.TotalServicos, o.TotalPecas, o.TotalGeral,
        o.CriadaEm, o.DiagnosticadaEm, o.EnviadaAprovacaoEm,
        o.OrcamentoAprovadoEm, o.OrcamentoRejeitadoEm,
        o.IniciadaEm, o.FinalizadaEm, o.EntregueEm,
        o.ItensServico.Select(ApresentarItemServico).ToList(),
        o.ItensPeca.Select(ApresentarItemPeca).ToList());

    public static ItemServicoResponse ApresentarItemServico(ItemServico i) =>
        new(i.Id, i.ServicoId, i.ServicoNome, i.PrecoSnapshot, i.Quantidade, i.Subtotal);

    public static ItemPecaResponse ApresentarItemPeca(ItemPeca i) =>
        new(i.Id, i.PecaId, i.PecaNome, i.PrecoSnapshot, i.Quantidade, i.Subtotal);

    public static PaginaOrdens ApresentarPagina(IReadOnlyList<OrdemDeServico> itens, int total, int pagina, int tamanhoPagina) =>
        new(itens.Select(Apresentar).ToList(), total, pagina, tamanhoPagina);

    // Converte TimeSpan -> double minutos (lógica antes em ObterTempoMedioExecucaoUseCase).
    public static MetricasTempoMedioResponse ApresentarMetrica(MetricaTempoMedio m) => new(
        m.TotalOrdensConcluidas,
        m.TempoMedio?.TotalMinutes,
        m.TempoMinimo?.TotalMinutes,
        m.TempoMaximo?.TotalMinutes);
}
```

- [ ] **Step 7: Escrever o teste do `OrdemDeServicoPresenter`**

Create `tests/Oficina.Adaptadores.Testes/OrdensServico/OrdemDeServicoPresenterTestes.cs`:
```csharp
using System.Linq;
using FluentAssertions;
using Oficina.Adaptadores.OrdensServico.Presenters;
using Oficina.Aplicacao.OrdensServico.Gateways;
using Oficina.Dominio.OrdensServico;

namespace Oficina.Adaptadores.Testes.OrdensServico;

public class OrdemDeServicoPresenterTestes
{
    private static OrdemDeServico CriarOrdemComItens()
    {
        var os = OrdemDeServico.Criar(Guid.NewGuid(), Guid.NewGuid(), "obs");
        os.AdicionarItemServico(Guid.NewGuid(), "Troca de óleo", 150m, 1);
        os.AdicionarItemPeca(Guid.NewGuid(), "Filtro", 25m, 2);
        return os;
    }

    [Fact]
    public void Apresentar_DeveMapearCamposEItens()
    {
        var os = CriarOrdemComItens();

        var resp = OrdemDeServicoPresenter.Apresentar(os);

        resp.Id.Should().Be(os.Id);
        resp.Status.Should().Be("Recebida");
        resp.Observacoes.Should().Be("obs");
        resp.TotalServicos.Should().Be(150m);
        resp.TotalPecas.Should().Be(50m);
        resp.TotalGeral.Should().Be(200m);
        resp.ItensServico.Should().ContainSingle(i => i.Nome == "Troca de óleo");
        resp.ItensPeca.Should().ContainSingle(i => i.Nome == "Filtro");
    }

    [Fact]
    public void ApresentarItemServico_DeveMapearCampos()
    {
        var os = CriarOrdemComItens();
        var item = os.ItensServico.First();

        var resp = OrdemDeServicoPresenter.ApresentarItemServico(item);

        resp.Id.Should().Be(item.Id);
        resp.ServicoId.Should().Be(item.ServicoId);
        resp.Nome.Should().Be("Troca de óleo");
        resp.PrecoUnitario.Should().Be(150m);
        resp.Quantidade.Should().Be(1);
        resp.Subtotal.Should().Be(150m);
    }

    [Fact]
    public void ApresentarItemPeca_DeveMapearCampos()
    {
        var os = CriarOrdemComItens();
        var item = os.ItensPeca.First();

        var resp = OrdemDeServicoPresenter.ApresentarItemPeca(item);

        resp.Id.Should().Be(item.Id);
        resp.PecaId.Should().Be(item.PecaId);
        resp.Nome.Should().Be("Filtro");
        resp.PrecoUnitario.Should().Be(25m);
        resp.Quantidade.Should().Be(2);
        resp.Subtotal.Should().Be(50m);
    }

    [Fact]
    public void ApresentarPagina_DeveMapearItensEMetadados()
    {
        var itens = new[] { CriarOrdemComItens() };

        var pagina = OrdemDeServicoPresenter.ApresentarPagina(itens, total: 1, pagina: 1, tamanhoPagina: 20);

        pagina.Total.Should().Be(1);
        pagina.Pagina.Should().Be(1);
        pagina.TamanhoPagina.Should().Be(20);
        pagina.Itens.Should().ContainSingle(o => o.Status == "Recebida");
    }

    [Fact]
    public void ApresentarMetrica_DeveConverterTimeSpanParaMinutos()
    {
        var metrica = new MetricaTempoMedio(
            TotalOrdensConcluidas: 3,
            TempoMedio: TimeSpan.FromMinutes(90),
            TempoMinimo: TimeSpan.FromMinutes(30),
            TempoMaximo: TimeSpan.FromMinutes(150));

        var resp = OrdemDeServicoPresenter.ApresentarMetrica(metrica);

        resp.TotalOrdensConcluidas.Should().Be(3);
        resp.TempoMedioMinutos.Should().Be(90);
        resp.TempoMinimoMinutos.Should().Be(30);
        resp.TempoMaximoMinutos.Should().Be(150);
    }

    [Fact]
    public void ApresentarMetrica_SemOrdens_DeveManterNulos()
    {
        var metrica = new MetricaTempoMedio(0, null, null, null);

        var resp = OrdemDeServicoPresenter.ApresentarMetrica(metrica);

        resp.TotalOrdensConcluidas.Should().Be(0);
        resp.TempoMedioMinutos.Should().BeNull();
        resp.TempoMinimoMinutos.Should().BeNull();
        resp.TempoMaximoMinutos.Should().BeNull();
    }
}
```

- [ ] **Step 8: Criar o `OrdemDeServicoController` de aplicação (Adaptadores)**

Create `src/Oficina.Adaptadores/OrdensServico/Controllers/OrdemDeServicoController.cs`:
```csharp
using Oficina.Adaptadores.OrdensServico.Presenters;
using Oficina.Aplicacao.OrdensServico;
using Oficina.Aplicacao.OrdensServico.Dtos;

namespace Oficina.Adaptadores.OrdensServico.Controllers;

// Controller de aplicação (Adaptadores de Interface): orquestra os casos de uso e formata via Presenter.
public class OrdemDeServicoController
{
    private readonly CriarOrdemUseCase _criar;
    private readonly ObterOrdemPorIdUseCase _obter;
    private readonly ListarOrdensUseCase _listar;
    private readonly IniciarDiagnosticoUseCase _iniciarDiagnostico;
    private readonly EnviarOrcamentoParaAprovacaoUseCase _enviarOrcamento;
    private readonly IniciarExecucaoUseCase _iniciarExecucao;
    private readonly FinalizarOrdemUseCase _finalizar;
    private readonly EntregarOrdemUseCase _entregar;
    private readonly AdicionarItemServicoUseCase _adicionarItemServico;
    private readonly RemoverItemServicoUseCase _removerItemServico;
    private readonly AdicionarItemPecaUseCase _adicionarItemPeca;
    private readonly RemoverItemPecaUseCase _removerItemPeca;
    private readonly ObterTempoMedioExecucaoUseCase _tempoMedio;

    public OrdemDeServicoController(
        CriarOrdemUseCase criar,
        ObterOrdemPorIdUseCase obter,
        ListarOrdensUseCase listar,
        IniciarDiagnosticoUseCase iniciarDiagnostico,
        EnviarOrcamentoParaAprovacaoUseCase enviarOrcamento,
        IniciarExecucaoUseCase iniciarExecucao,
        FinalizarOrdemUseCase finalizar,
        EntregarOrdemUseCase entregar,
        AdicionarItemServicoUseCase adicionarItemServico,
        RemoverItemServicoUseCase removerItemServico,
        AdicionarItemPecaUseCase adicionarItemPeca,
        RemoverItemPecaUseCase removerItemPeca,
        ObterTempoMedioExecucaoUseCase tempoMedio)
    {
        _criar = criar;
        _obter = obter;
        _listar = listar;
        _iniciarDiagnostico = iniciarDiagnostico;
        _enviarOrcamento = enviarOrcamento;
        _iniciarExecucao = iniciarExecucao;
        _finalizar = finalizar;
        _entregar = entregar;
        _adicionarItemServico = adicionarItemServico;
        _removerItemServico = removerItemServico;
        _adicionarItemPeca = adicionarItemPeca;
        _removerItemPeca = removerItemPeca;
        _tempoMedio = tempoMedio;
    }

    public async Task<OrdemResponse> CriarAsync(CriarOrdemRequest req, CancellationToken ct)
    {
        var ordem = await _criar.ExecutarAsync(req, ct);
        return OrdemDeServicoPresenter.Apresentar(ordem);
    }

    public async Task<OrdemResponse?> ObterPorIdAsync(Guid id, CancellationToken ct)
    {
        var ordem = await _obter.ExecutarAsync(id, ct);
        return ordem is null ? null : OrdemDeServicoPresenter.Apresentar(ordem);
    }

    public async Task<PaginaOrdens> ListarAsync(string? status, int pagina, int tamanhoPagina, CancellationToken ct)
    {
        var r = await _listar.ExecutarAsync(status, pagina, tamanhoPagina, ct);
        return OrdemDeServicoPresenter.ApresentarPagina(r.Itens, r.Total, r.Pagina, r.TamanhoPagina);
    }

    public async Task<OrdemResponse?> IniciarDiagnosticoAsync(Guid id, CancellationToken ct)
    {
        var ordem = await _iniciarDiagnostico.ExecutarAsync(id, ct);
        return ordem is null ? null : OrdemDeServicoPresenter.Apresentar(ordem);
    }

    public async Task<OrdemResponse?> EnviarOrcamentoParaAprovacaoAsync(Guid id, CancellationToken ct)
    {
        var ordem = await _enviarOrcamento.ExecutarAsync(id, ct);
        return ordem is null ? null : OrdemDeServicoPresenter.Apresentar(ordem);
    }

    public async Task<OrdemResponse?> IniciarExecucaoAsync(Guid id, CancellationToken ct)
    {
        var ordem = await _iniciarExecucao.ExecutarAsync(id, ct);
        return ordem is null ? null : OrdemDeServicoPresenter.Apresentar(ordem);
    }

    public async Task<OrdemResponse?> FinalizarAsync(Guid id, CancellationToken ct)
    {
        var ordem = await _finalizar.ExecutarAsync(id, ct);
        return ordem is null ? null : OrdemDeServicoPresenter.Apresentar(ordem);
    }

    public async Task<OrdemResponse?> EntregarAsync(Guid id, CancellationToken ct)
    {
        var ordem = await _entregar.ExecutarAsync(id, ct);
        return ordem is null ? null : OrdemDeServicoPresenter.Apresentar(ordem);
    }

    public async Task<ItemServicoResponse?> AdicionarItemServicoAsync(Guid ordemId, AdicionarItemServicoRequest req, CancellationToken ct)
    {
        var item = await _adicionarItemServico.ExecutarAsync(ordemId, req, ct);
        return item is null ? null : OrdemDeServicoPresenter.ApresentarItemServico(item);
    }

    public Task<bool> RemoverItemServicoAsync(Guid ordemId, Guid itemId, CancellationToken ct) =>
        _removerItemServico.ExecutarAsync(ordemId, itemId, ct);

    public async Task<ItemPecaResponse?> AdicionarItemPecaAsync(Guid ordemId, AdicionarItemPecaRequest req, CancellationToken ct)
    {
        var item = await _adicionarItemPeca.ExecutarAsync(ordemId, req, ct);
        return item is null ? null : OrdemDeServicoPresenter.ApresentarItemPeca(item);
    }

    public Task<bool> RemoverItemPecaAsync(Guid ordemId, Guid itemId, CancellationToken ct) =>
        _removerItemPeca.ExecutarAsync(ordemId, itemId, ct);

    public async Task<MetricasTempoMedioResponse> ObterTempoMedioExecucaoAsync(CancellationToken ct)
    {
        var metrica = await _tempoMedio.ExecutarAsync(ct);
        return OrdemDeServicoPresenter.ApresentarMetrica(metrica);
    }
}
```

- [ ] **Step 9: Criar o `OrdemDeServicoDataSource` (Infraestrutura) com o corpo do antigo repositório**

**CRÍTICO:** copiar o corpo do antigo `OrdemDeServicoRepositorio` EXATAMENTE — em especial os `.Include(o => o.ItensServico).Include(o => o.ItensPeca)`, `EmTransacaoSerializadaAsync` (`BeginTransactionAsync(IsolationLevel.Serializable)` + Commit/Rollback/Dispose), `ObterTempoMedioExecucaoAsync` (contagem + materialização das datas + cálculo client-side em milissegundos → `TimeSpan`), `MarcarItem*ComoNovo` (`_db.Set<...>().Add`), o filtro por status e a paginação com clamp. Usa `using Oficina.Aplicacao.OrdensServico.Gateways;` para o record `MetricaTempoMedio` (novo lar).

Create `src/Oficina.Infraestrutura/Persistencia/DataSources/OrdemDeServicoDataSource.cs`:
```csharp
using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Oficina.Adaptadores.OrdensServico.DataSources;
using Oficina.Aplicacao.OrdensServico.Gateways;
using Oficina.Dominio.OrdensServico;

namespace Oficina.Infraestrutura.Persistencia.DataSources;

public class OrdemDeServicoDataSource : IOrdemDeServicoDataSource
{
    private readonly OficinaDbContext _db;
    public OrdemDeServicoDataSource(OficinaDbContext db) => _db = db;

    public Task<OrdemDeServico?> ObterPorIdAsync(Guid id, CancellationToken ct) =>
        _db.OrdensServico
            .Include(o => o.ItensServico)
            .Include(o => o.ItensPeca)
            .FirstOrDefaultAsync(o => o.Id == id, ct);

    public Task<OrdemDeServico?> ObterPorNumeroAsync(long numero, CancellationToken ct) =>
        _db.OrdensServico
            .Include(o => o.ItensServico)
            .Include(o => o.ItensPeca)
            .FirstOrDefaultAsync(o => o.Numero == numero, ct);

    public async Task<IReadOnlyList<OrdemDeServico>> ListarAsync(
        StatusOrdemDeServico? status, int pagina, int tamanhoPagina, CancellationToken ct)
    {
        if (pagina < 1) pagina = 1;
        if (tamanhoPagina < 1 || tamanhoPagina > 100) tamanhoPagina = 20;

        var q = _db.OrdensServico
            .Include(o => o.ItensServico)
            .Include(o => o.ItensPeca)
            .AsQueryable();
        if (status.HasValue) q = q.Where(o => o.Status == status.Value);

        return await q
            .OrderByDescending(o => o.CriadaEm)
            .Skip((pagina - 1) * tamanhoPagina)
            .Take(tamanhoPagina)
            .ToListAsync(ct);
    }

    public Task<int> ContarAsync(StatusOrdemDeServico? status, CancellationToken ct)
    {
        var q = _db.OrdensServico.AsQueryable();
        if (status.HasValue) q = q.Where(o => o.Status == status.Value);
        return q.CountAsync(ct);
    }

    public async Task AdicionarAsync(OrdemDeServico ordem, CancellationToken ct) =>
        await _db.OrdensServico.AddAsync(ordem, ct);

    public Task SalvarAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);

    public void MarcarItemServicoComoNovo(ItemServico item) =>
        _db.Set<ItemServico>().Add(item);

    public void MarcarItemPecaComoNovo(ItemPeca item) =>
        _db.Set<ItemPeca>().Add(item);

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

    public async Task<MetricaTempoMedio> ObterTempoMedioExecucaoAsync(CancellationToken ct)
    {
        // Considera OSs que tiveram início e fim de execução (Finalizada ou Entregue)
        var concluidas = _db.OrdensServico
            .Where(o => o.IniciadaEm != null && o.FinalizadaEm != null);

        var total = await concluidas.CountAsync(ct);
        if (total == 0)
            return new MetricaTempoMedio(0, null, null, null);

        var datas = await concluidas
            .Select(o => new { o.IniciadaEm, o.FinalizadaEm })
            .ToListAsync(ct);

        var duracoes = datas
            .Select(d => (d.FinalizadaEm!.Value - d.IniciadaEm!.Value).TotalMilliseconds)
            .ToList();

        return new MetricaTempoMedio(
            TotalOrdensConcluidas: total,
            TempoMedio: TimeSpan.FromMilliseconds(duracoes.Average()),
            TempoMinimo: TimeSpan.FromMilliseconds(duracoes.Min()),
            TempoMaximo: TimeSpan.FromMilliseconds(duracoes.Max()));
    }
}
```

- [ ] **Step 10: Deletar o antigo repositório e a antiga interface de repositório de Ordens de Serviço**

Run:
```bash
cd "C:/Users/thiago/source/repos/Fiap/fiap_15SOAT_fase1"
rm src/Oficina.Infraestrutura/Persistencia/Repositorios/OrdemDeServicoRepositorio.cs
rm src/Oficina.Dominio/OrdensServico/IOrdemDeServicoRepositorio.cs
```

> `IOrdemDeServicoRepositorio.cs` era o antigo lar do record `MetricaTempoMedio`. Ele já foi recriado em `IOrdemDeServicoGateway.cs` (Step 1), no namespace `Oficina.Aplicacao.OrdensServico.Gateways`; nenhum arquivo remanescente referencia o `Oficina.Dominio.OrdensServico.MetricaTempoMedio` (o DataSource e o use case de métrica usam o novo namespace).

- [ ] **Step 11: Migrar os 3 casos de uso cross-context (Consulta) para `IOrdemDeServicoGateway`**

Os 3 use cases de Consulta usam `_ordens.ObterPorNumeroAsync` e `_ordens.SalvarAsync` (ambos existem em `IOrdemDeServicoGateway`). Vivem no assembly `Oficina.Aplicacao`, então referenciam `Oficina.Aplicacao.OrdensServico.Gateways`. A montagem inline de `ConsultaPublicaResponse` e o comportamento anti-enumeração **não mudam**. Mantêm o `IClienteGateway` já migrado (Plano 02).

Substituir `src/Oficina.Aplicacao/Consulta/ConsultarOrdemPorNumeroUseCase.cs`:
```csharp
using Oficina.Aplicacao.Clientes.Gateways;
using Oficina.Aplicacao.Consulta.Dtos;
using Oficina.Aplicacao.OrdensServico.Gateways;
using Oficina.Dominio.Clientes;

namespace Oficina.Aplicacao.Consulta;

public class ConsultarOrdemPorNumeroUseCase
{
    private readonly IOrdemDeServicoGateway _ordens;
    private readonly IClienteGateway _clientes;

    public ConsultarOrdemPorNumeroUseCase(
        IOrdemDeServicoGateway ordens, IClienteGateway clientes)
    {
        _ordens = ordens;
        _clientes = clientes;
    }

    public async Task<ResultadoConsulta> ExecutarAsync(long numero, string documentoBruto, CancellationToken ct)
    {
        // Valida documento sem lançar — assinatura uniforme
        Documento? doc = null;
        try { doc = Documento.Criar(documentoBruto); }
        catch (DocumentoInvalidoException) { return new ResultadoConsulta.NaoEncontrada(); }

        var os = await _ordens.ObterPorNumeroAsync(numero, ct);
        if (os is null) return new ResultadoConsulta.NaoEncontrada();

        var cliente = await _clientes.ObterPorIdAsync(os.ClienteId, ct);
        if (cliente is null) return new ResultadoConsulta.NaoEncontrada();

        if (!cliente.Documento.Equals(doc))
            return new ResultadoConsulta.DocumentoNaoConfere();

        var veiculo = cliente.Veiculos.FirstOrDefault(v => v.Id == os.VeiculoId)
            ?? throw new InvalidOperationException("Inconsistência: veículo da OS não pertence ao cliente.");

        var itens = new List<ItemConsultaResponse>();
        itens.AddRange(os.ItensServico.Select(i =>
            new ItemConsultaResponse("Servico", i.ServicoNome, i.PrecoSnapshot, i.Quantidade, i.Subtotal)));
        itens.AddRange(os.ItensPeca.Select(i =>
            new ItemConsultaResponse("Peca", i.PecaNome, i.PrecoSnapshot, i.Quantidade, i.Subtotal)));

        var resp = new ConsultaPublicaResponse(
            os.Numero, os.Status.ToString(),
            cliente.Nome, cliente.Documento.Mascarado(),
            veiculo.Placa.Valor, $"{veiculo.Marca} {veiculo.Modelo} ({veiculo.Ano})",
            os.TotalServicos, os.TotalPecas, os.TotalGeral,
            os.CriadaEm, os.EnviadaAprovacaoEm, os.OrcamentoAprovadoEm, os.OrcamentoRejeitadoEm,
            os.IniciadaEm, os.FinalizadaEm, os.EntregueEm,
            itens);

        return new ResultadoConsulta.Sucesso(resp);
    }
}
```

Substituir `src/Oficina.Aplicacao/Consulta/AprovarOrcamentoPorClienteUseCase.cs` (mantém o `using Oficina.Dominio.OrdensServico;` — o helper `ConstruirRespostaAsync` referencia `OrdemDeServico` por tipo explícito):
```csharp
using Oficina.Aplicacao.Clientes.Gateways;
using Oficina.Aplicacao.Consulta.Dtos;
using Oficina.Aplicacao.OrdensServico.Gateways;
using Oficina.Dominio.Clientes;
using Oficina.Dominio.OrdensServico;

namespace Oficina.Aplicacao.Consulta;

public class AprovarOrcamentoPorClienteUseCase
{
    private readonly IOrdemDeServicoGateway _ordens;
    private readonly IClienteGateway _clientes;

    public AprovarOrcamentoPorClienteUseCase(
        IOrdemDeServicoGateway ordens, IClienteGateway clientes)
    {
        _ordens = ordens;
        _clientes = clientes;
    }

    public async Task<ResultadoConsulta> ExecutarAsync(long numero, string documentoBruto, CancellationToken ct)
    {
        Documento? doc = null;
        try { doc = Documento.Criar(documentoBruto); }
        catch (DocumentoInvalidoException) { return new ResultadoConsulta.NaoEncontrada(); }

        var os = await _ordens.ObterPorNumeroAsync(numero, ct);
        if (os is null) return new ResultadoConsulta.NaoEncontrada();

        var cliente = await _clientes.ObterPorIdAsync(os.ClienteId, ct);
        if (cliente is null) return new ResultadoConsulta.NaoEncontrada();

        if (!cliente.Documento.Equals(doc))
            return new ResultadoConsulta.DocumentoNaoConfere();

        os.Aprovar();
        await _ordens.SalvarAsync(ct);

        return new ResultadoConsulta.Sucesso(
            await ConstruirRespostaAsync(os, cliente, ct));
    }

    private static async Task<ConsultaPublicaResponse> ConstruirRespostaAsync(
        OrdemDeServico os, Cliente cliente, CancellationToken ct)
    {
        await Task.CompletedTask; // assinatura assíncrona reservada para futura agregação
        var veiculo = cliente.Veiculos.First(v => v.Id == os.VeiculoId);

        var itens = new List<ItemConsultaResponse>();
        itens.AddRange(os.ItensServico.Select(i =>
            new ItemConsultaResponse("Servico", i.ServicoNome, i.PrecoSnapshot, i.Quantidade, i.Subtotal)));
        itens.AddRange(os.ItensPeca.Select(i =>
            new ItemConsultaResponse("Peca", i.PecaNome, i.PrecoSnapshot, i.Quantidade, i.Subtotal)));

        return new ConsultaPublicaResponse(
            os.Numero, os.Status.ToString(),
            cliente.Nome, cliente.Documento.Mascarado(),
            veiculo.Placa.Valor, $"{veiculo.Marca} {veiculo.Modelo} ({veiculo.Ano})",
            os.TotalServicos, os.TotalPecas, os.TotalGeral,
            os.CriadaEm, os.EnviadaAprovacaoEm, os.OrcamentoAprovadoEm, os.OrcamentoRejeitadoEm,
            os.IniciadaEm, os.FinalizadaEm, os.EntregueEm,
            itens);
    }
}
```

Substituir `src/Oficina.Aplicacao/Consulta/RejeitarOrcamentoPorClienteUseCase.cs`:
```csharp
using Oficina.Aplicacao.Clientes.Gateways;
using Oficina.Aplicacao.Consulta.Dtos;
using Oficina.Aplicacao.OrdensServico.Gateways;
using Oficina.Dominio.Clientes;

namespace Oficina.Aplicacao.Consulta;

public class RejeitarOrcamentoPorClienteUseCase
{
    private readonly IOrdemDeServicoGateway _ordens;
    private readonly IClienteGateway _clientes;

    public RejeitarOrcamentoPorClienteUseCase(
        IOrdemDeServicoGateway ordens, IClienteGateway clientes)
    {
        _ordens = ordens;
        _clientes = clientes;
    }

    public async Task<ResultadoConsulta> ExecutarAsync(long numero, string documentoBruto, CancellationToken ct)
    {
        Documento? doc = null;
        try { doc = Documento.Criar(documentoBruto); }
        catch (DocumentoInvalidoException) { return new ResultadoConsulta.NaoEncontrada(); }

        var os = await _ordens.ObterPorNumeroAsync(numero, ct);
        if (os is null) return new ResultadoConsulta.NaoEncontrada();

        var cliente = await _clientes.ObterPorIdAsync(os.ClienteId, ct);
        if (cliente is null) return new ResultadoConsulta.NaoEncontrada();

        if (!cliente.Documento.Equals(doc))
            return new ResultadoConsulta.DocumentoNaoConfere();

        os.Rejeitar();
        await _ordens.SalvarAsync(ct);

        var veiculo = cliente.Veiculos.First(v => v.Id == os.VeiculoId);
        var itens = new List<ItemConsultaResponse>();
        itens.AddRange(os.ItensServico.Select(i =>
            new ItemConsultaResponse("Servico", i.ServicoNome, i.PrecoSnapshot, i.Quantidade, i.Subtotal)));
        itens.AddRange(os.ItensPeca.Select(i =>
            new ItemConsultaResponse("Peca", i.PecaNome, i.PrecoSnapshot, i.Quantidade, i.Subtotal)));

        return new ResultadoConsulta.Sucesso(new ConsultaPublicaResponse(
            os.Numero, os.Status.ToString(),
            cliente.Nome, cliente.Documento.Mascarado(),
            veiculo.Placa.Valor, $"{veiculo.Marca} {veiculo.Modelo} ({veiculo.Ano})",
            os.TotalServicos, os.TotalPecas, os.TotalGeral,
            os.CriadaEm, os.EnviadaAprovacaoEm, os.OrcamentoAprovadoEm, os.OrcamentoRejeitadoEm,
            os.IniciadaEm, os.FinalizadaEm, os.EntregueEm,
            itens));
    }
}
```

- [ ] **Step 12: Ajustar o wiring de DI (Infra e Adaptadores)**

Substituir `src/Oficina.Infraestrutura/DependencyInjectionRepositorios.cs` (remove o binding `IOrdemDeServicoRepositorio`/`OrdemDeServicoRepositorio` e o `using Oficina.Dominio.OrdensServico;` que só servia a ele; adiciona o `IOrdemDeServicoDataSource`):
```csharp
using Microsoft.Extensions.DependencyInjection;
using Oficina.Adaptadores.Catalogo.DataSources;
using Oficina.Adaptadores.Clientes.DataSources;
using Oficina.Adaptadores.Estoque.DataSources;
using Oficina.Adaptadores.OrdensServico.DataSources;
using Oficina.Dominio.Auth;
using Oficina.Infraestrutura.Persistencia.DataSources;
using Oficina.Infraestrutura.Persistencia.Repositorios;

namespace Oficina.Infraestrutura;

public static class DependencyInjectionRepositorios
{
    public static IServiceCollection AdicionarRepositorios(this IServiceCollection services)
    {
        services.AddScoped<IUsuarioRepositorio, UsuarioRepositorio>();

        // DataSources (Clean Architecture — Frameworks & Drivers)
        services.AddScoped<IServicoDataSource, ServicoDataSource>();
        services.AddScoped<IClienteDataSource, ClienteDataSource>();
        services.AddScoped<IPecaDataSource, PecaDataSource>();
        services.AddScoped<IOrdemDeServicoDataSource, OrdemDeServicoDataSource>();
        return services;
    }
}
```

Substituir `src/Oficina.Adaptadores/DependencyInjectionAdaptadores.cs` (adiciona o Gateway e o Controller de Ordens de Serviço):
```csharp
using Microsoft.Extensions.DependencyInjection;
using Oficina.Adaptadores.Catalogo.Controllers;
using Oficina.Adaptadores.Catalogo.Gateways;
using Oficina.Adaptadores.Clientes.Controllers;
using Oficina.Adaptadores.Clientes.Gateways;
using Oficina.Adaptadores.Estoque.Controllers;
using Oficina.Adaptadores.Estoque.Gateways;
using Oficina.Adaptadores.OrdensServico.Controllers;
using Oficina.Adaptadores.OrdensServico.Gateways;
using Oficina.Aplicacao.Catalogo.Gateways;
using Oficina.Aplicacao.Clientes.Gateways;
using Oficina.Aplicacao.Estoque.Gateways;
using Oficina.Aplicacao.OrdensServico.Gateways;

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

        // Ordens de Serviço
        services.AddScoped<IOrdemDeServicoGateway, OrdemDeServicoGateway>();
        services.AddScoped<OrdemDeServicoController>();
        return services;
    }
}
```

> Nota: os 13 use cases de Ordens de Serviço já estão registrados em `Oficina.Aplicacao/DependencyInjectionAplicacao.cs` (Scoped, linhas 49-61) e **não mudam** — o `OrdemDeServicoController` os recebe por injeção. Os 3 use cases de Consulta também continuam registrados lá (linhas 64-66) e agora resolvem `IOrdemDeServicoGateway` (registrado em `AdicionarAdaptadores`), pois `Program.cs` compõe `AdicionarAplicacao()` + `AdicionarAdaptadores()` + `AdicionarInfraestrutura()` no mesmo container.

- [ ] **Step 13: Deixar o controller HTTP fino (delegando ao `OrdemDeServicoController`)**

Substituir `src/Oficina.Api/Controllers/OrdensServicoController.cs` (rotas, verbos e status preservados exatamente; em especial `Created(string.Empty, resp)` na adição de itens, `CreatedAtAction(nameof(Obter), ...)` na criação, e a política `RequerAdmin` na métrica):
```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oficina.Adaptadores.OrdensServico.Controllers;
using Oficina.Aplicacao.OrdensServico.Dtos;
using Oficina.Api.Configuracao;

namespace Oficina.Api.Controllers;

[ApiController]
[Route("api/v1/ordens-servico")]
[Authorize(Policy = PoliticasDeAutorizacao.RequerAdminOuAtendente)]
public class OrdensServicoController : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Criar(
        [FromBody] CriarOrdemRequest req,
        [FromServices] OrdemDeServicoController controller,
        CancellationToken ct)
    {
        var resp = await controller.CriarAsync(req, ct);
        return CreatedAtAction(nameof(Obter), new { id = resp.Id }, resp);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Obter(
        Guid id,
        [FromServices] OrdemDeServicoController controller,
        CancellationToken ct)
    {
        var resp = await controller.ObterPorIdAsync(id, ct);
        return resp is null ? NotFound() : Ok(resp);
    }

    [HttpGet]
    public async Task<IActionResult> Listar(
        [FromServices] OrdemDeServicoController controller,
        [FromQuery] string? status = null,
        [FromQuery] int pagina = 1,
        [FromQuery] int tamanhoPagina = 20,
        CancellationToken ct = default)
    {
        var resp = await controller.ListarAsync(status, pagina, tamanhoPagina, ct);
        return Ok(resp);
    }

    // ===== Itens =====

    [HttpPost("{id:guid}/servicos")]
    public async Task<IActionResult> AdicionarServico(
        Guid id,
        [FromBody] AdicionarItemServicoRequest req,
        [FromServices] OrdemDeServicoController controller,
        CancellationToken ct)
    {
        var resp = await controller.AdicionarItemServicoAsync(id, req, ct);
        return resp is null ? NotFound() : Created(string.Empty, resp);
    }

    [HttpDelete("{id:guid}/servicos/{itemId:guid}")]
    public async Task<IActionResult> RemoverServico(
        Guid id, Guid itemId,
        [FromServices] OrdemDeServicoController controller,
        CancellationToken ct)
    {
        var ok = await controller.RemoverItemServicoAsync(id, itemId, ct);
        return ok ? NoContent() : NotFound();
    }

    [HttpPost("{id:guid}/pecas")]
    public async Task<IActionResult> AdicionarPeca(
        Guid id,
        [FromBody] AdicionarItemPecaRequest req,
        [FromServices] OrdemDeServicoController controller,
        CancellationToken ct)
    {
        var resp = await controller.AdicionarItemPecaAsync(id, req, ct);
        return resp is null ? NotFound() : Created(string.Empty, resp);
    }

    [HttpDelete("{id:guid}/pecas/{itemId:guid}")]
    public async Task<IActionResult> RemoverPeca(
        Guid id, Guid itemId,
        [FromServices] OrdemDeServicoController controller,
        CancellationToken ct)
    {
        var ok = await controller.RemoverItemPecaAsync(id, itemId, ct);
        return ok ? NoContent() : NotFound();
    }

    // ===== Transições =====

    [HttpPatch("{id:guid}/diagnostico")]
    public async Task<IActionResult> IniciarDiagnostico(
        Guid id,
        [FromServices] OrdemDeServicoController controller,
        CancellationToken ct)
    {
        var resp = await controller.IniciarDiagnosticoAsync(id, ct);
        return resp is null ? NotFound() : Ok(resp);
    }

    [HttpPost("{id:guid}/orcamento/enviar")]
    public async Task<IActionResult> EnviarParaAprovacao(
        Guid id,
        [FromServices] OrdemDeServicoController controller,
        CancellationToken ct)
    {
        var resp = await controller.EnviarOrcamentoParaAprovacaoAsync(id, ct);
        return resp is null ? NotFound() : Ok(resp);
    }

    [HttpPost("{id:guid}/execucao/iniciar")]
    public async Task<IActionResult> IniciarExecucao(
        Guid id,
        [FromServices] OrdemDeServicoController controller,
        CancellationToken ct)
    {
        var resp = await controller.IniciarExecucaoAsync(id, ct);
        return resp is null ? NotFound() : Ok(resp);
    }

    [HttpPost("{id:guid}/finalizar")]
    public async Task<IActionResult> Finalizar(
        Guid id,
        [FromServices] OrdemDeServicoController controller,
        CancellationToken ct)
    {
        var resp = await controller.FinalizarAsync(id, ct);
        return resp is null ? NotFound() : Ok(resp);
    }

    [HttpPost("{id:guid}/entregar")]
    public async Task<IActionResult> Entregar(
        Guid id,
        [FromServices] OrdemDeServicoController controller,
        CancellationToken ct)
    {
        var resp = await controller.EntregarAsync(id, ct);
        return resp is null ? NotFound() : Ok(resp);
    }

    // ===== Métricas =====

    [HttpGet("metricas/tempo-medio")]
    [Authorize(Policy = PoliticasDeAutorizacao.RequerAdmin)]
    public async Task<IActionResult> TempoMedioExecucao(
        [FromServices] OrdemDeServicoController controller,
        CancellationToken ct)
    {
        var resp = await controller.ObterTempoMedioExecucaoAsync(ct);
        return Ok(resp);
    }
}
```

> Nota: o HTTP controller (`Oficina.Api.Controllers.OrdensServicoController`) e o controller de aplicação (`Oficina.Adaptadores.OrdensServico.Controllers.OrdemDeServicoController`) têm nomes diferentes e namespaces diferentes — sem colisão. O `[FromServices] OrdemDeServicoController controller` resolve o de Adaptadores via `using Oficina.Adaptadores.OrdensServico.Controllers;`.

- [ ] **Step 14: Adaptar os testes de use case de Ordens de Serviço (mock `IOrdemDeServicoGateway`; assert em entidades)**

Substituir `tests/Oficina.Aplicacao.Testes/OrdensServico/CriarOrdemUseCaseTestes.cs` (retorno vira `OrdemDeServico`; `Status` vira enum):
```csharp
using FluentAssertions;
using Moq;
using Oficina.Aplicacao.Clientes.Gateways;
using Oficina.Aplicacao.OrdensServico;
using Oficina.Aplicacao.OrdensServico.Dtos;
using Oficina.Aplicacao.OrdensServico.Gateways;
using Oficina.Dominio.Clientes;
using Oficina.Dominio.OrdensServico;
using Xunit;

namespace Oficina.Aplicacao.Testes.OrdensServico;

public class CriarOrdemUseCaseTestes
{
    private readonly Mock<IOrdemDeServicoGateway> _gateway = new();
    private readonly Mock<IClienteGateway> _clientes = new();

    [Fact]
    public async Task Executar_ComClienteEVeiculoValidos_DeveCriar()
    {
        var cliente = Cliente.Criar("J", Documento.Criar("39053344705"),
            Email.Criar("e@x.com"), Telefone.Criar("11987654321"));
        var veiculo = Veiculo.Criar(Placa.Criar("ABC1234"), "F", "U", 2020);
        cliente.AdicionarVeiculo(veiculo);

        _clientes.Setup(c => c.ObterPorIdAsync(cliente.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cliente);

        // simular o re-fetch após criação
        _gateway.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) =>
                OrdemDeServico.Criar(cliente.Id, veiculo.Id));

        var ordem = await new CriarOrdemUseCase(_gateway.Object, _clientes.Object)
            .ExecutarAsync(new CriarOrdemRequest(cliente.Id, veiculo.Id, "obs"), default);

        ordem.Status.Should().Be(StatusOrdemDeServico.Recebida);
        _gateway.Verify(r => r.AdicionarAsync(It.IsAny<OrdemDeServico>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Executar_ComClienteInexistente_DeveLancar()
    {
        _clientes.Setup(c => c.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Cliente?)null);

        var act = async () => await new CriarOrdemUseCase(_gateway.Object, _clientes.Object)
            .ExecutarAsync(new CriarOrdemRequest(Guid.NewGuid(), Guid.NewGuid(), null), default);

        await act.Should().ThrowAsync<OrdemInvalidaException>().WithMessage("*Cliente*");
    }

    [Fact]
    public async Task Executar_ComVeiculoQueNaoPertenceAoCliente_DeveLancar()
    {
        var cliente = Cliente.Criar("J", Documento.Criar("39053344705"),
            Email.Criar("e@x.com"), Telefone.Criar("11987654321"));
        cliente.AdicionarVeiculo(Veiculo.Criar(Placa.Criar("ABC1234"), "F", "U", 2020));

        _clientes.Setup(c => c.ObterPorIdAsync(cliente.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cliente);

        var act = async () => await new CriarOrdemUseCase(_gateway.Object, _clientes.Object)
            .ExecutarAsync(new CriarOrdemRequest(cliente.Id, Guid.NewGuid(), null), default);

        await act.Should().ThrowAsync<OrdemInvalidaException>().WithMessage("*Veículo*");
    }
}
```

Substituir `tests/Oficina.Aplicacao.Testes/OrdensServico/IniciarExecucaoUseCaseTestes.cs` (troca o mock de ordens; o retorno vira `OrdemDeServico?` — os asserts de `NotBeNull`/`BeNull`, status e saldo seguem válidos):
```csharp
using FluentAssertions;
using Moq;
using Oficina.Aplicacao.Estoque.Gateways;
using Oficina.Aplicacao.OrdensServico;
using Oficina.Aplicacao.OrdensServico.Gateways;
using Oficina.Dominio.Estoque;
using Oficina.Dominio.OrdensServico;
using Xunit;

namespace Oficina.Aplicacao.Testes.OrdensServico;

public class IniciarExecucaoUseCaseTestes
{
    private readonly Mock<IOrdemDeServicoGateway> _ordens = new();
    private readonly Mock<IPecaGateway> _pecas = new();

    private void TransacaoIdentidade()
    {
        _ordens.Setup(r => r.EmTransacaoSerializadaAsync(
            It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>()))
            .Returns<Func<CancellationToken, Task>, CancellationToken>((f, ct) => f(ct));
    }

    private static OrdemDeServico OsAprovadaCom(Peca peca, int qtd)
    {
        var os = OrdemDeServico.Criar(Guid.NewGuid(), Guid.NewGuid());
        os.AdicionarItemPeca(peca.Id, peca.Nome, peca.PrecoUnitario, qtd);
        os.IniciarDiagnostico();
        os.EnviarOrcamentoParaAprovacao();
        os.Aprovar();
        return os;
    }

    [Fact]
    public async Task Executar_ComEstoqueSuficiente_DeveIniciarExecucaoEBaixarEstoque()
    {
        var peca = Peca.Criar(Sku.Criar("ABC-123"), "Filtro", 25m);
        peca.RegistrarEntrada(10, "compra inicial");
        var os = OsAprovadaCom(peca, 3);

        _ordens.Setup(r => r.ObterPorIdAsync(os.Id, It.IsAny<CancellationToken>())).ReturnsAsync(os);
        _pecas.Setup(p => p.ObterPorIdAsync(peca.Id, It.IsAny<CancellationToken>())).ReturnsAsync(peca);
        TransacaoIdentidade();

        var resp = await new IniciarExecucaoUseCase(_ordens.Object, _pecas.Object)
            .ExecutarAsync(os.Id, default);

        resp.Should().NotBeNull();
        os.Status.Should().Be(StatusOrdemDeServico.EmExecucao);
        peca.SaldoAtual.Should().Be(7);
    }

    [Fact]
    public async Task Executar_ComEstoqueInsuficiente_DeveLancarSemAlterarStatusOrdemNemSaldo()
    {
        var peca = Peca.Criar(Sku.Criar("ABC-123"), "Filtro", 25m);
        peca.RegistrarEntrada(2, "compra");
        var os = OsAprovadaCom(peca, 5);

        _ordens.Setup(r => r.ObterPorIdAsync(os.Id, It.IsAny<CancellationToken>())).ReturnsAsync(os);
        _pecas.Setup(p => p.ObterPorIdAsync(peca.Id, It.IsAny<CancellationToken>())).ReturnsAsync(peca);
        TransacaoIdentidade();

        var act = async () => await new IniciarExecucaoUseCase(_ordens.Object, _pecas.Object)
            .ExecutarAsync(os.Id, default);

        await act.Should().ThrowAsync<SaldoInsuficienteException>();
        peca.SaldoAtual.Should().Be(2);
    }

    [Fact]
    public async Task Executar_OsNaoAprovada_DeveLancar()
    {
        var os = OrdemDeServico.Criar(Guid.NewGuid(), Guid.NewGuid());
        os.AdicionarItemServico(Guid.NewGuid(), "S", 10m, 1);
        os.IniciarDiagnostico();
        os.EnviarOrcamentoParaAprovacao();
        // não aprovada

        _ordens.Setup(r => r.ObterPorIdAsync(os.Id, It.IsAny<CancellationToken>())).ReturnsAsync(os);
        TransacaoIdentidade();

        var act = async () => await new IniciarExecucaoUseCase(_ordens.Object, _pecas.Object)
            .ExecutarAsync(os.Id, default);

        await act.Should().ThrowAsync<OrcamentoNaoAprovadoException>();
    }

    [Fact]
    public async Task Executar_OsInexistente_DeveRetornarNull()
    {
        _ordens.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((OrdemDeServico?)null);
        TransacaoIdentidade();

        var resp = await new IniciarExecucaoUseCase(_ordens.Object, _pecas.Object)
            .ExecutarAsync(Guid.NewGuid(), default);

        resp.Should().BeNull();
    }
}
```

Substituir `tests/Oficina.Aplicacao.Testes/OrdensServico/AdicionarItensUseCaseTestes.cs` (troca o mock de ordens; os use cases agora retornam `ItemServico?`/`ItemPeca?` — os asserts passam a ler as propriedades da entidade: `ServicoNome`/`PecaNome`, `PrecoSnapshot`, `Subtotal`):
```csharp
using FluentAssertions;
using Moq;
using Oficina.Aplicacao.Catalogo.Gateways;
using Oficina.Aplicacao.Estoque.Gateways;
using Oficina.Aplicacao.OrdensServico;
using Oficina.Aplicacao.OrdensServico.Dtos;
using Oficina.Aplicacao.OrdensServico.Gateways;
using Oficina.Dominio.Catalogo;
using Oficina.Dominio.Estoque;
using Oficina.Dominio.OrdensServico;
using Xunit;

namespace Oficina.Aplicacao.Testes.OrdensServico;

public class AdicionarItensUseCaseTestes
{
    [Fact]
    public async Task AdicionarItemServico_DeveTirarSnapshotDoNomeEPreco()
    {
        var ordem = OrdemDeServico.Criar(Guid.NewGuid(), Guid.NewGuid());
        var serv = Servico.Criar("Troca de óleo", "x", 150m, 30);

        var ordens = new Mock<IOrdemDeServicoGateway>();
        ordens.Setup(r => r.ObterPorIdAsync(ordem.Id, It.IsAny<CancellationToken>())).ReturnsAsync(ordem);
        var servicos = new Mock<IServicoGateway>();
        servicos.Setup(s => s.ObterPorIdAsync(serv.Id, It.IsAny<CancellationToken>())).ReturnsAsync(serv);

        var item = await new AdicionarItemServicoUseCase(ordens.Object, servicos.Object)
            .ExecutarAsync(ordem.Id, new AdicionarItemServicoRequest(serv.Id, 2), default);

        item.Should().NotBeNull();
        item!.ServicoNome.Should().Be("Troca de óleo");
        item.PrecoSnapshot.Should().Be(150m);
        item.Subtotal.Should().Be(300m);
    }

    [Fact]
    public async Task AdicionarItemPeca_DeveTirarSnapshotDoNomeEPreco()
    {
        var ordem = OrdemDeServico.Criar(Guid.NewGuid(), Guid.NewGuid());
        var peca = Peca.Criar(Sku.Criar("ABC-123"), "Filtro", 25m);

        var ordens = new Mock<IOrdemDeServicoGateway>();
        ordens.Setup(r => r.ObterPorIdAsync(ordem.Id, It.IsAny<CancellationToken>())).ReturnsAsync(ordem);
        var pecas = new Mock<IPecaGateway>();
        pecas.Setup(p => p.ObterPorIdAsync(peca.Id, It.IsAny<CancellationToken>())).ReturnsAsync(peca);

        var item = await new AdicionarItemPecaUseCase(ordens.Object, pecas.Object)
            .ExecutarAsync(ordem.Id, new AdicionarItemPecaRequest(peca.Id, 4), default);

        item.Should().NotBeNull();
        item!.PecaNome.Should().Be("Filtro");
        item.PrecoSnapshot.Should().Be(25m);
        item.Subtotal.Should().Be(100m);
    }

    [Fact]
    public async Task AdicionarItemServico_ServicoInativo_DeveLancar()
    {
        var ordem = OrdemDeServico.Criar(Guid.NewGuid(), Guid.NewGuid());
        var serv = Servico.Criar("S", "x", 10m, 30);
        serv.Inativar();

        var ordens = new Mock<IOrdemDeServicoGateway>();
        ordens.Setup(r => r.ObterPorIdAsync(ordem.Id, It.IsAny<CancellationToken>())).ReturnsAsync(ordem);
        var servicos = new Mock<IServicoGateway>();
        servicos.Setup(s => s.ObterPorIdAsync(serv.Id, It.IsAny<CancellationToken>())).ReturnsAsync(serv);

        var act = async () => await new AdicionarItemServicoUseCase(ordens.Object, servicos.Object)
            .ExecutarAsync(ordem.Id, new AdicionarItemServicoRequest(serv.Id, 1), default);

        await act.Should().ThrowAsync<OrdemInvalidaException>().WithMessage("*inativo*");
    }
}
```

- [ ] **Step 15: Adaptar os testes cross-context de Consulta (mock `IOrdemDeServicoGateway`)**

Em cada arquivo abaixo, aplicar a **mesma transformação mecânica**: (a) adicionar `using Oficina.Aplicacao.OrdensServico.Gateways;` (manter `using Oficina.Dominio.OrdensServico;` — ambos usam `OrdemDeServico.Criar` e exceções de transição por tipo explícito); (b) trocar `Mock<IOrdemDeServicoRepositorio>` por `Mock<IOrdemDeServicoGateway>`. Nenhum cenário muda (ambos só usam `ObterPorNumeroAsync` e `SalvarAsync`, que existem em `IOrdemDeServicoGateway`; a resposta segue montada inline pelos use cases).

Em `tests/Oficina.Aplicacao.Testes/Consulta/ConsultarOrdemPorNumeroUseCaseTestes.cs`:
- Adicionar (junto aos demais `using`): `using Oficina.Aplicacao.OrdensServico.Gateways;`
- Trocar `private readonly Mock<IOrdemDeServicoRepositorio> _ordens = new();` por `private readonly Mock<IOrdemDeServicoGateway> _ordens = new();`

Em `tests/Oficina.Aplicacao.Testes/Consulta/AprovarOrcamentoPorClienteUseCaseTestes.cs`:
- Adicionar (junto aos demais `using`): `using Oficina.Aplicacao.OrdensServico.Gateways;`
- Trocar `private readonly Mock<IOrdemDeServicoRepositorio> _ordens = new();` por `private readonly Mock<IOrdemDeServicoGateway> _ordens = new();`

- [ ] **Step 16: Escrever o teste do `OrdemDeServicoGateway` (delegação, incl. `EmTransacaoSerializadaAsync` e `ObterTempoMedioExecucaoAsync`)**

Create `tests/Oficina.Adaptadores.Testes/OrdensServico/OrdemDeServicoGatewayTestes.cs`:
```csharp
using FluentAssertions;
using Moq;
using Oficina.Adaptadores.OrdensServico.DataSources;
using Oficina.Adaptadores.OrdensServico.Gateways;
using Oficina.Aplicacao.OrdensServico.Gateways;
using Oficina.Dominio.OrdensServico;

namespace Oficina.Adaptadores.Testes.OrdensServico;

public class OrdemDeServicoGatewayTestes
{
    private static OrdemDeServico CriarOrdem() =>
        OrdemDeServico.Criar(Guid.NewGuid(), Guid.NewGuid());

    [Fact]
    public async Task ObterPorIdAsync_DeveDelegarParaODataSource()
    {
        var ds = new Mock<IOrdemDeServicoDataSource>();
        var esperada = CriarOrdem();
        ds.Setup(d => d.ObterPorIdAsync(esperada.Id, It.IsAny<CancellationToken>())).ReturnsAsync(esperada);

        var gateway = new OrdemDeServicoGateway(ds.Object);
        var obtida = await gateway.ObterPorIdAsync(esperada.Id, default);

        obtida.Should().BeSameAs(esperada);
        ds.Verify(d => d.ObterPorIdAsync(esperada.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ObterPorNumeroAsync_DeveDelegarParaODataSource()
    {
        var ds = new Mock<IOrdemDeServicoDataSource>();
        var esperada = CriarOrdem();
        ds.Setup(d => d.ObterPorNumeroAsync(42, It.IsAny<CancellationToken>())).ReturnsAsync(esperada);

        var gateway = new OrdemDeServicoGateway(ds.Object);
        var obtida = await gateway.ObterPorNumeroAsync(42, default);

        obtida.Should().BeSameAs(esperada);
        ds.Verify(d => d.ObterPorNumeroAsync(42, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AdicionarESalvar_DevemDelegarParaODataSource()
    {
        var ds = new Mock<IOrdemDeServicoDataSource>();
        var gateway = new OrdemDeServicoGateway(ds.Object);
        var ordem = CriarOrdem();

        await gateway.AdicionarAsync(ordem, default);
        await gateway.SalvarAsync(default);

        ds.Verify(d => d.AdicionarAsync(ordem, It.IsAny<CancellationToken>()), Times.Once);
        ds.Verify(d => d.SalvarAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ListarEContar_DevemDelegarParaODataSource()
    {
        var ds = new Mock<IOrdemDeServicoDataSource>();
        var lista = new[] { CriarOrdem() };
        ds.Setup(d => d.ListarAsync(null, 1, 20, It.IsAny<CancellationToken>())).ReturnsAsync(lista);
        ds.Setup(d => d.ContarAsync(null, It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var gateway = new OrdemDeServicoGateway(ds.Object);
        var itens = await gateway.ListarAsync(null, 1, 20, default);
        var total = await gateway.ContarAsync(null, default);

        itens.Should().BeSameAs(lista);
        total.Should().Be(1);
    }

    [Fact]
    public void MarcarItensComoNovos_DevemDelegarParaODataSource()
    {
        var ds = new Mock<IOrdemDeServicoDataSource>();
        var gateway = new OrdemDeServicoGateway(ds.Object);
        var ordem = CriarOrdem();
        var itemServico = ordem.AdicionarItemServico(Guid.NewGuid(), "S", 10m, 1);
        var itemPeca = ordem.AdicionarItemPeca(Guid.NewGuid(), "P", 5m, 1);

        gateway.MarcarItemServicoComoNovo(itemServico);
        gateway.MarcarItemPecaComoNovo(itemPeca);

        ds.Verify(d => d.MarcarItemServicoComoNovo(itemServico), Times.Once);
        ds.Verify(d => d.MarcarItemPecaComoNovo(itemPeca), Times.Once);
    }

    [Fact]
    public async Task EmTransacaoSerializadaAsync_DeveDelegarParaODataSource()
    {
        var ds = new Mock<IOrdemDeServicoDataSource>();
        Func<CancellationToken, Task> acao = _ => Task.CompletedTask;
        ds.Setup(d => d.EmTransacaoSerializadaAsync(acao, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var gateway = new OrdemDeServicoGateway(ds.Object);
        await gateway.EmTransacaoSerializadaAsync(acao, default);

        ds.Verify(d => d.EmTransacaoSerializadaAsync(acao, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ObterTempoMedioExecucaoAsync_DeveDelegarParaODataSource()
    {
        var ds = new Mock<IOrdemDeServicoDataSource>();
        var metrica = new MetricaTempoMedio(2, TimeSpan.FromMinutes(60), TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(90));
        ds.Setup(d => d.ObterTempoMedioExecucaoAsync(It.IsAny<CancellationToken>())).ReturnsAsync(metrica);

        var gateway = new OrdemDeServicoGateway(ds.Object);
        var obtida = await gateway.ObterTempoMedioExecucaoAsync(default);

        obtida.Should().BeSameAs(metrica);
        ds.Verify(d => d.ObterTempoMedioExecucaoAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
```

- [ ] **Step 17: Escrever o teste do `OrdemDeServicoController` (orquestração + Presenter)**

Create `tests/Oficina.Adaptadores.Testes/OrdensServico/OrdemDeServicoControllerTestes.cs`:
```csharp
using FluentAssertions;
using Moq;
using Oficina.Adaptadores.OrdensServico.Controllers;
using Oficina.Aplicacao.Catalogo.Gateways;
using Oficina.Aplicacao.Clientes.Gateways;
using Oficina.Aplicacao.Estoque.Gateways;
using Oficina.Aplicacao.OrdensServico;
using Oficina.Aplicacao.OrdensServico.Dtos;
using Oficina.Aplicacao.OrdensServico.Gateways;
using Oficina.Dominio.Catalogo;
using Oficina.Dominio.Clientes;
using Oficina.Dominio.OrdensServico;

namespace Oficina.Adaptadores.Testes.OrdensServico;

public class OrdemDeServicoControllerTestes
{
    private static OrdemDeServicoController CriarController(
        Mock<IOrdemDeServicoGateway> ordens,
        Mock<IClienteGateway> clientes,
        Mock<IServicoGateway> servicos,
        Mock<IPecaGateway> pecas) =>
        new(
            new CriarOrdemUseCase(ordens.Object, clientes.Object),
            new ObterOrdemPorIdUseCase(ordens.Object),
            new ListarOrdensUseCase(ordens.Object),
            new IniciarDiagnosticoUseCase(ordens.Object),
            new EnviarOrcamentoParaAprovacaoUseCase(ordens.Object),
            new IniciarExecucaoUseCase(ordens.Object, pecas.Object),
            new FinalizarOrdemUseCase(ordens.Object),
            new EntregarOrdemUseCase(ordens.Object),
            new AdicionarItemServicoUseCase(ordens.Object, servicos.Object),
            new RemoverItemServicoUseCase(ordens.Object),
            new AdicionarItemPecaUseCase(ordens.Object, pecas.Object),
            new RemoverItemPecaUseCase(ordens.Object),
            new ObterTempoMedioExecucaoUseCase(ordens.Object));

    [Fact]
    public async Task CriarAsync_DeveRetornarOrdemResponseFormatada()
    {
        var ordens = new Mock<IOrdemDeServicoGateway>();
        var clientes = new Mock<IClienteGateway>();
        var servicos = new Mock<IServicoGateway>();
        var pecas = new Mock<IPecaGateway>();

        var cliente = Cliente.Criar("J", Documento.Criar("39053344705"),
            Email.Criar("e@x.com"), Telefone.Criar("11987654321"));
        var veiculo = Veiculo.Criar(Placa.Criar("ABC1234"), "F", "U", 2020);
        cliente.AdicionarVeiculo(veiculo);
        clientes.Setup(c => c.ObterPorIdAsync(cliente.Id, It.IsAny<CancellationToken>())).ReturnsAsync(cliente);
        ordens.Setup(o => o.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OrdemDeServico.Criar(cliente.Id, veiculo.Id));

        var controller = CriarController(ordens, clientes, servicos, pecas);
        var resp = await controller.CriarAsync(new CriarOrdemRequest(cliente.Id, veiculo.Id, null), default);

        resp.Should().BeOfType<OrdemResponse>();
        resp.Status.Should().Be("Recebida");
        ordens.Verify(o => o.AdicionarAsync(It.IsAny<OrdemDeServico>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ObterPorIdAsync_QuandoNaoExiste_DeveRetornarNull()
    {
        var ordens = new Mock<IOrdemDeServicoGateway>();
        ordens.Setup(o => o.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((OrdemDeServico?)null);
        var controller = CriarController(ordens, new Mock<IClienteGateway>(),
            new Mock<IServicoGateway>(), new Mock<IPecaGateway>());

        var resp = await controller.ObterPorIdAsync(Guid.NewGuid(), default);

        resp.Should().BeNull();
    }

    [Fact]
    public async Task ListarAsync_DeveMontarPaginaOrdens()
    {
        var ordens = new Mock<IOrdemDeServicoGateway>();
        var lista = new[] { OrdemDeServico.Criar(Guid.NewGuid(), Guid.NewGuid()) };
        ordens.Setup(o => o.ListarAsync(null, 1, 20, It.IsAny<CancellationToken>())).ReturnsAsync(lista);
        ordens.Setup(o => o.ContarAsync(null, It.IsAny<CancellationToken>())).ReturnsAsync(1);
        var controller = CriarController(ordens, new Mock<IClienteGateway>(),
            new Mock<IServicoGateway>(), new Mock<IPecaGateway>());

        var pagina = await controller.ListarAsync(null, 1, 20, default);

        pagina.Total.Should().Be(1);
        pagina.Itens.Should().ContainSingle(o => o.Status == "Recebida");
    }

    [Fact]
    public async Task AdicionarItemServicoAsync_DeveRetornarItemServicoResponse()
    {
        var ordens = new Mock<IOrdemDeServicoGateway>();
        var servicos = new Mock<IServicoGateway>();
        var ordem = OrdemDeServico.Criar(Guid.NewGuid(), Guid.NewGuid());
        var serv = Servico.Criar("Troca de óleo", "x", 150m, 30);
        ordens.Setup(o => o.ObterPorIdAsync(ordem.Id, It.IsAny<CancellationToken>())).ReturnsAsync(ordem);
        servicos.Setup(s => s.ObterPorIdAsync(serv.Id, It.IsAny<CancellationToken>())).ReturnsAsync(serv);
        var controller = CriarController(ordens, new Mock<IClienteGateway>(), servicos, new Mock<IPecaGateway>());

        var resp = await controller.AdicionarItemServicoAsync(
            ordem.Id, new AdicionarItemServicoRequest(serv.Id, 2), default);

        resp.Should().NotBeNull();
        resp!.Nome.Should().Be("Troca de óleo");
        resp.Subtotal.Should().Be(300m);
        ordens.Verify(o => o.MarcarItemServicoComoNovo(It.IsAny<ItemServico>()), Times.Once);
    }

    [Fact]
    public async Task ObterTempoMedioExecucaoAsync_DeveConverterParaMinutos()
    {
        var ordens = new Mock<IOrdemDeServicoGateway>();
        ordens.Setup(o => o.ObterTempoMedioExecucaoAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MetricaTempoMedio(2, TimeSpan.FromMinutes(60), TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(90)));
        var controller = CriarController(ordens, new Mock<IClienteGateway>(),
            new Mock<IServicoGateway>(), new Mock<IPecaGateway>());

        var resp = await controller.ObterTempoMedioExecucaoAsync(default);

        resp.TotalOrdensConcluidas.Should().Be(2);
        resp.TempoMedioMinutos.Should().Be(60);
        resp.TempoMinimoMinutos.Should().Be(30);
        resp.TempoMaximoMinutos.Should().Be(90);
    }
}
```

- [ ] **Step 18: Verificar que não sobraram referências à interface deletada nem ao mapeador**

Run:
```bash
cd "C:/Users/thiago/source/repos/Fiap/fiap_15SOAT_fase1"
grep -rn "IOrdemDeServicoRepositorio\|MapeadorOrdem" src tests --include=*.cs
```
Expected: **nenhuma linha** de saída (todas as referências migraram para `IOrdemDeServicoGateway`/`OrdemDeServicoPresenter`). Se algo aparecer, corrigir migrando para `IOrdemDeServicoGateway` (mesmo assembly `Oficina.Aplicacao`) antes de prosseguir.

- [ ] **Step 19: Compilar a solution**

Run: `dotnet build Oficina.sln`
Expected: **Build succeeded**, 0 erros (todas as referências resolvidas; deleções sem referências pendentes; `MetricaTempoMedio` resolvido apenas via `Oficina.Aplicacao.OrdensServico.Gateways`).

- [ ] **Step 20: Rodar os testes unitários — um projeto por chamada (toolchain .NET 10)**

Run (uma chamada por projeto; passar múltiplos projetos falha com `MSB1008`):
```bash
dotnet test tests/Oficina.Dominio.Testes
dotnet test tests/Oficina.Aplicacao.Testes
dotnet test tests/Oficina.Adaptadores.Testes
```
Expected: PASS em todos (inclui os testes adaptados de Ordens de Serviço/Consulta e os novos testes de Presenter/Gateway/Controller de Ordens de Serviço).

- [ ] **Step 21: (CI apenas) Testes de integração — NÃO rodar localmente (Docker indisponível)**

Os testes `tests/Oficina.Integracao.Testes/OrdensServico/OrdensServicoFluxoTestes.cs` (fluxo completo Recebida → Entregue + baixa de estoque transacional na execução), `tests/Oficina.Integracao.Testes/OrdensServico/MetricasTestes.cs` (endpoint de métricas com `RequerAdmin`) e `tests/Oficina.Integracao.Testes/Consulta/ConsultaPublicaTestes.cs` (rota pública anti-enumeração + aprovar/rejeitar) são a rede de segurança do refactor. Eles usam Testcontainers + Postgres e rodam **no CI**. Localmente, apenas registrar que o gate local (Steps 19–20) passou; **não** executar `dotnet test tests/Oficina.Integracao.Testes` sem Docker.

- [ ] **Step 22: Commit**

```bash
cd "C:/Users/thiago/source/repos/Fiap/fiap_15SOAT_fase1"
git add -A
git commit -m "$(cat <<'EOF'
refactor: Ordens de Servico para Clean Architecture (Gateway/DataSource/Presenter/Controller)

Os 13 casos de uso de Ordens de Servico passam a consumir IOrdemDeServicoGateway e a
retornar entidades; OrdemDeServicoController (aplicacao) orquestra e formata via
OrdemDeServicoPresenter (incl. a conversao TimeSpan->minutos da metrica, antes no use
case). OrdemDeServicoGateway delega para IOrdemDeServicoDataSource, implementado por
OrdemDeServicoDataSource (EF Core) — preserva Include(ItensServico/ItensPeca), a
transacao serializavel (EmTransacaoSerializadaAsync), ObterTempoMedioExecucaoAsync,
MarcarItem*ComoNovo, filtro por status e paginacao com clamp. O record MetricaTempoMedio
muda de lar para o namespace do gateway (Oficina.Aplicacao.OrdensServico.Gateways).
OrdemInvalidaException vai para arquivo proprio. Controller HTTP fica fino (preserva
rotas/verbos/status, incl. Created(string.Empty) e a politica RequerAdmin na metrica).
Os 3 casos de uso de Consulta migram de IOrdemDeServicoRepositorio para
IOrdemDeServicoGateway. Remove IOrdemDeServicoRepositorio, OrdemDeServicoRepositorio e
MapeadorOrdem. Contrato HTTP inalterado.

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Auto-revisão do plano (cobertura vs. contexto Ordens de Serviço + Consulta)

- **Cobertura de arquivos:** os 13 casos de uso de Ordens de Serviço, os 3 casos de uso de Consulta, os 7 DTOs (inalterados), os 3 validators (inalterados — continuam válidos na fronteira da Aplicação), o repositório de infra, a configuração EF, os 2 HTTP controllers e a interface de domínio + mapeador estão todos endereçados. DTOs (`OrdemResponse`, `ItemServicoResponse`, `ItemPecaResponse`, `MetricasTempoMedioResponse`) e Validators **não mudam** (contrato e regras de validação de entrada idênticos). O `ConsultaController` HTTP **não muda** (só as dependências internas dos use cases). ✔
- **Parte A (remoção dos Domain Events) e impacto nos testes de Domínio:** os eventos eram código morto (grep confirmou uso só em `OrdemDeServico`, `ExcecoesOrdemServico`, `OrdemDeServicoConfiguration`, `CriarOrdemUseCase`, `IniciarExecucaoUseCase` e `OrdemDeServicoTestes`). Task 1 remove tudo e ajusta o único teste que assertava `EventosNaoPublicados` (`IniciarExecucao_AposAprovacao_DeveAvancarEDispararEvento` → `..._DeveAvancar`), preservando a asserção da transição (`Status == EmExecucao`, `IniciadaEm != null`). O grep de verificação (Step 7 da Task 1) cobre inclusive as variações de nome (`IniciadaEvent` casa `ExecucaoIniciadaEvent`, etc.). Task 1 termina com build + Domínio + Aplicação verdes. ✔
- **Particularidades vs. o template do Estoque (desvios conscientes):**
  1. **Novo lar de `MetricaTempoMedio`** — o record saiu de `Oficina.Dominio.OrdensServico/IOrdemDeServicoRepositorio.cs` (deletado) para `Oficina.Aplicacao.OrdensServico/Gateways/IOrdemDeServicoGateway.cs` (namespace `Oficina.Aplicacao.OrdensServico.Gateways`). Os consumidores foram ajustados: `ObterTempoMedioExecucaoUseCase` (usa o namespace do gateway), `OrdemDeServicoDataSource` (Infra, `using Oficina.Aplicacao.OrdensServico.Gateways;`), `OrdemDeServicoGateway`, `IOrdemDeServicoDataSource` e o Presenter. Durante o interim (antes de deletar a interface antiga) coexistem dois `MetricaTempoMedio` em namespaces distintos, mas nenhum arquivo importa os dois → sem ambiguidade; ao final só resta o novo. ✔
  2. **Métrica movida para o Presenter** — a conversão `TimeSpan? → double? minutos` (antes em `ObterTempoMedioExecucaoUseCase`, produzindo `MetricasTempoMedioResponse`) migrou para `OrdemDeServicoPresenter.ApresentarMetrica(MetricaTempoMedio) → MetricasTempoMedioResponse` (via `?.TotalMinutes`). O use case agora só retorna `MetricaTempoMedio` (entidade de métrica). Há testes dedicados: `OrdemDeServicoPresenterTestes.ApresentarMetrica_*` e `OrdemDeServicoControllerTestes.ObterTempoMedioExecucaoAsync_DeveConverterParaMinutos`. ✔
  3. **Transação serializável e includes preservados** — `IOrdemDeServicoGateway`/`IOrdemDeServicoDataSource` mantêm `EmTransacaoSerializadaAsync`; `OrdemDeServicoDataSource` copia EXATAMENTE `BeginTransactionAsync(IsolationLevel.Serializable)` + Commit/Rollback/Dispose (via `System.Data` + `Microsoft.EntityFrameworkCore.Storage`), o `.Include(ItensServico).Include(ItensPeca)` em `ObterPorId`/`ObterPorNumero`/`Listar`, `ObterTempoMedioExecucaoAsync` com cálculo client-side em milissegundos, `MarcarItem*ComoNovo` (`_db.Set<...>().Add`), o filtro por status e a paginação com clamp. Há testes de delegação para `EmTransacaoSerializadaAsync` e `ObterTempoMedioExecucaoAsync` no `OrdemDeServicoGatewayTestes`. ✔
  4. **Preservação dos gateways já migrados** — `IniciarExecucaoUseCase`/`AdicionarItemPecaUseCase` continuam usando `IPecaGateway` (Plano 03) e `AdicionarItemServicoUseCase` continua usando `IServicoGateway` (Plano 01); só o `IOrdemDeServicoRepositorio` deles vira `IOrdemDeServicoGateway`. A transação serializável usada por `IniciarExecucaoUseCase` é a de **ordens** (`_ordens.EmTransacaoSerializadaAsync`), inalterada. ✔
  5. **Cross-context Consulta** — os 3 use cases migram de `IOrdemDeServicoRepositorio` para `IOrdemDeServicoGateway` (mesmo assembly). A montagem inline de `ConsultaPublicaResponse` (não é o `MapeadorOrdem`) e o comportamento anti-enumeração (404 idêntico para OS inexistente e documento que não confere) **não mudam**. `AprovarOrcamentoPorClienteUseCase` mantém `using Oficina.Dominio.OrdensServico;` (o helper `ConstruirRespostaAsync` referencia `OrdemDeServico` por tipo); os outros dois dropam esse `using` (não referenciam tipo de domínio de OS explicitamente). 2 dos 3 têm teste unitário (`Consultar`, `Aprovar`); ambos adaptados. ✔
  6. **`Created(string.Empty, resp)` e políticas** — o HTTP controller preserva EXATAMENTE os `Created(string.Empty, resp)` de `AdicionarServico`/`AdicionarPeca` (201 sem Location resolvível; item/OS inexistente → 404 pelo `null`), o `CreatedAtAction(nameof(Obter), ...)` na criação, os 200/404 das transições e do GET, os 204/404 dos DELETE, e a política extra `[Authorize(Policy = RequerAdmin)]` na métrica sobre a política de classe `RequerAdminOuAtendente`. ✔
  7. **`OrdemInvalidaException` em arquivo próprio** — decisão: **mover** (limpeza). Fica no mesmo namespace `Oficina.Aplicacao.OrdensServico`, então nenhum consumidor (`ListarOrdensUseCase`, `AdicionarItemServicoUseCase`, `AdicionarItemPecaUseCase`, `IniciarExecucaoUseCase`, testes) muda de `using`. Removida da declaração inline em `CriarOrdemUseCase.cs`. ✔
  8. **`PaginaOrdens` mantida** — o record `PaginaOrdens` continua no namespace `Oficina.Aplicacao.OrdensServico` (contrato inalterado) e ganha o vizinho `ResultadoListaOrdens` (entidades), espelhando `PaginaPecas`/`ResultadoListaPecas`. ✔
- **Consistência de tipos/nomes entre passos:** assinaturas de `IOrdemDeServicoGateway` ≡ `IOrdemDeServicoDataSource` (incl. `ObterPorNumeroAsync`, `EmTransacaoSerializadaAsync`, `ObterTempoMedioExecucaoAsync`, `MarcarItemServicoComoNovo`, `MarcarItemPecaComoNovo`); retornos dos use cases (`OrdemDeServico`/`OrdemDeServico?`/`bool`/`ItemServico?`/`ItemPeca?`/`ResultadoListaOrdens`/`MetricaTempoMedio`) batem com o consumo no `OrdemDeServicoController` e nos testes; `OrdemDeServicoPresenter.Apresentar` usa exatamente os mesmos campos do antigo `MapeadorOrdem.Mapear` (todos os 8 timestamps + totais + itens), `ApresentarItemServico`/`ApresentarItemPeca` os de `MapearServ`/`MapearPeca` (`ServicoNome`/`PecaNome`, `PrecoSnapshot`, `Quantidade`, `Subtotal`); os asserts adaptados em `AdicionarItensUseCaseTestes` passaram de `resp.Nome`/`resp.PrecoUnitario` (Response) para `item.ServicoNome`/`item.PecaNome`/`item.PrecoSnapshot` (entidade); o `CriarOrdemUseCaseTestes` passou de `resp.Status == "Recebida"` (string) para `ordem.Status == StatusOrdemDeServico.Recebida` (enum). Métodos do `OrdemDeServicoController` (`AdicionarItemServicoAsync`, `IniciarDiagnosticoAsync`, etc.) batem 1:1 com as chamadas do HTTP controller. ✔
- **Sem placeholders:** todo código é completo; caminhos e comandos são absolutos/exatos; os comandos de teste respeitam a regra "um projeto por chamada"; os testes de Adaptadores omitem `using Xunit;` (global via `<Using Include="Xunit" />` no `.csproj`) e os de Aplicação o incluem (convenção existente). ✔
- **Regra de dependência:** `IOrdemDeServicoGateway` + `MetricaTempoMedio` moram em `Oficina.Aplicacao` (para os use cases cross-context de Consulta consumirem sem `Oficina.Aplicacao` depender de `Oficina.Adaptadores`); `IOrdemDeServicoDataSource` mora em `Oficina.Adaptadores`; `OrdemDeServicoDataSource` (Infra) implementa a interface de Adaptadores e usa `MetricaTempoMedio` de `Oficina.Aplicacao` (Infra → Aplicacao é permitido). `Oficina.Dominio` continua sem dependências externas. ✔
- **DI:** os 13 use cases de Ordens de Serviço e os 3 de Consulta já estão registrados em `DependencyInjectionAplicacao.cs` (linhas 49-66) e não mudam; `AdicionarAdaptadores` ganha `IOrdemDeServicoGateway→OrdemDeServicoGateway` + `OrdemDeServicoController`; `AdicionarRepositorios` troca `IOrdemDeServicoRepositorio→OrdemDeServicoRepositorio` por `IOrdemDeServicoDataSource→OrdemDeServicoDataSource` (e dropa o `using Oficina.Dominio.OrdensServico;` que só servia à interface antiga). ✔
