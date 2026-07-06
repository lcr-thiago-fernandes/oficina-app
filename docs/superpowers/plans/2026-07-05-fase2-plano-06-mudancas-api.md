# Fase 2 — Plano 06: Mudanças de API (comportamento novo sobre a base Clean Architecture)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recomendado) ou superpowers:executing-plans para implementar este plano task-a-task. Os passos usam checkbox (`- [ ]`) para acompanhamento. Cada Task termina com **build + os 3 projetos de teste unitário verdes** e **um commit**.

**Goal:** Adicionar **quatro mudanças de comportamento novo** à API (não é refactor), preservando a Clean Architecture do curso (Casos de Uso em `Oficina.Aplicacao` consomem `IXxxGateway`; `XxxController` de Adaptadores orquestra use cases + `XxxPresenter`; `XxxGateway` delega p/ `IXxxDataSource`; `XxxDataSource` EF em `Oficina.Infraestrutura`; controllers HTTP finos em `Oficina.Api`):

1. **Notificação de status por e-mail (mock)** — nova porta `INotificacaoGateway` (em `Oficina.Aplicacao`) com impl mock que loga; as 5 transições de OS notificam após persistir.
2. **Listagem ordenada** — `GET /api/v1/ordens-servico` sem filtro exclui OS terminais (`Finalizada`/`Entregue`/`Cancelada`) e ordena por prioridade de status e depois por `CriadaEm` ascendente.
3. **Abertura de OS consolidada** — `POST /api/v1/ordens-servico` passa a receber `AbrirOrdemRequest` (cliente + veículo + serviços + peças) e abre tudo atomicamente (find-or-create).
4. **Webhook de aprovação externa** — novo `POST /api/v1/ordens-servico/{id}/orcamento/aprovacao` `[AllowAnonymous]` protegido por token de header, substituindo as rotas públicas `approve`/`reject` do `ConsultaController`.

**Architecture:** Cada mudança respeita a regra de dependência do curso (`Oficina.Api → Oficina.Adaptadores → Oficina.Aplicacao → Oficina.Dominio`; `Oficina.Infraestrutura → Oficina.Adaptadores → Oficina.Aplicacao → Oficina.Dominio`; `Oficina.Api → Oficina.Infraestrutura` só para wiring de DI). Portas novas (`INotificacaoGateway`) moram em `Oficina.Aplicacao`; a impl mock em `Oficina.Infraestrutura`. A lógica pura de filtro/ordenação da listagem é extraída para um helper estático em `Oficina.Adaptadores` (testável sem banco); o `OrdemDeServicoDataSource` (Infra) apenas a consome dentro da query EF. O caso de uso de abertura consolidada vive em `Oficina.Aplicacao` e depende dos quatro gateways (`IClienteGateway`, `IServicoGateway`, `IPecaGateway`, `IOrdemDeServicoGateway`), que compartilham o mesmo `OficinaDbContext` scoped — um único `SalvarAsync` no fim commita tudo atomicamente. A validação do token de webhook é dividida: a comparação pura (fixed-time, fail-closed) fica num helper de `Oficina.Adaptadores` (unit-testável), e o controller HTTP em `Oficina.Api` lê o header/config e retorna 401. Exceções de domínio continuam sendo lançadas pelos casos de uso e traduzidas por `MiddlewareDeExcecoes` — **nunca** tratadas nos controllers.

**Tech Stack:** C# 12 / .NET 8 (`net8.0`, `Nullable=enable`, `ImplicitUsings=enable`), ASP.NET Core, EF Core 8 + Npgsql, FluentValidation 11, Serilog, xUnit 2.5.3 + FluentAssertions 6.12.1 + Moq 4.20.72; integração via Testcontainers.PostgreSql (só no CI). Projetos e projetos de teste **já existem** (Planos 01–05) — não há scaffold. `Microsoft.Extensions.Logging.Abstractions` (ILogger) e `Microsoft.Extensions.Options` já disponíveis em `Oficina.Infraestrutura` (transitivamente via Hosting.Abstractions; ver `BootstrapAdminHostedService`/`GeradorTokenJwt`).

## Global Constraints

- **Idioma pt-BR** em código, identificadores, comentários e mensagens de commit.
- **Regra de dependência** (curso), como acima. `Oficina.Dominio` continua **sem dependências externas**. `Oficina.Aplicacao` **não** referencia `Oficina.Adaptadores` — por isso `INotificacaoGateway` mora em `Oficina.Aplicacao`.
- **DI idiomático**: classes stateless registradas Scoped, sem `new`/`static` manual de dependências (exceto helpers puros e testes unitários).
- **Contrato HTTP preservado onde não muda.** Onde muda, o request novo é o contrato:
  - Task 3 muda `POST /api/v1/ordens-servico` (corpo `CriarOrdemRequest` → `AbrirOrdemRequest`); mantém 201 `CreatedAtAction(nameof(Obter), new { id }, resp)`.
  - Task 4 adiciona `POST /api/v1/ordens-servico/{id:guid}/orcamento/aprovacao` (`[AllowAnonymous]`) e **remove** `POST /api/v1/consulta/{numeroOs}/aprovar` e `/rejeitar`; `GET /api/v1/consulta/{numeroOs}` e a anti-enumeração (404 idêntico para OS inexistente e documento que não confere) **permanecem**.
  - Endpoints granulares de item (`POST/DELETE {id}/servicos` e `/pecas`), transições (`diagnostico`/`orcamento/enviar`/`execucao/iniciar`/`finalizar`/`entregar`) e `metricas/tempo-medio` **não mudam**.
- **Não tratar exceções de domínio nos controllers** — `MiddlewareDeExcecoes` mapeia (`TransicaoDeStatusInvalidaException`, `OrdemSemItensException`, `OrcamentoNaoAprovadoException`, `OrdemImutavelException`, `ItemNaoEncontradoException`, `SaldoInsuficienteException`, `OrdemInvalidaException`, `DocumentoInvalidoException`, `PlacaInvalidaException`, `EmailInvalidoException`, `ArgumentException` → 422; `PlacaJaCadastradaException`/`DocumentoJaCadastradoException` → 409; etc.).
- **Docker indisponível no ambiente local** → **NÃO** rodar `Oficina.Integracao.Testes` (rodam no CI). Gate local por Task = `dotnet build Oficina.sln` (0 erros) + os 3 projetos de teste unitário verdes (`Oficina.Dominio.Testes`, `Oficina.Aplicacao.Testes`, `Oficina.Adaptadores.Testes`).
- **Toolchain .NET 10** no ambiente: rodar **um projeto de teste por chamada** de `dotnet test` (múltiplos projetos falham com `MSB1008`).
- **Cobertura de linha ≥ 80%** no CI (gate). DTO/Request/Response já são excluídos por `coverlet.runsettings`.
- **Bash tool = Git Bash** (POSIX sh): usar `rm`, `grep`, heredoc `<<'EOF'` — **não** usar sintaxe PowerShell.
- **Branch:** `fase-2`. Commits em pt-BR (um por Task) terminando com:
  `Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>`

## Estrutura de arquivos (o que este plano cria/modifica/deleta)

```
# ---- Task 1: Notificação de status por e-mail (mock) ----
src/Oficina.Aplicacao/
  OrdensServico/Gateways/INotificacaoGateway.cs                 ★NOVO (porta de notificação)
  OrdensServico/IniciarDiagnosticoUseCase.cs                    MODIFICADO (+ INotificacaoGateway; notifica após SalvarAsync)
  OrdensServico/EnviarOrcamentoParaAprovacaoUseCase.cs          MODIFICADO (idem)
  OrdensServico/IniciarExecucaoUseCase.cs                       MODIFICADO (idem; notifica após a transação)
  OrdensServico/FinalizarOrdemUseCase.cs                        MODIFICADO (idem)
  OrdensServico/EntregarOrdemUseCase.cs                         MODIFICADO (idem)
src/Oficina.Infraestrutura/
  Notificacoes/NotificacaoEmailMock.cs                          ★NOVO (impl mock que loga via ILogger)
  Notificacoes/DependencyInjectionNotificacoes.cs               ★NOVO (AdicionarNotificacoes)
  DependencyInjection.cs                                        MODIFICADO (chama AdicionarNotificacoes)
tests/Oficina.Aplicacao.Testes/
  OrdensServico/NotificacaoTransicaoUseCaseTestes.cs            ★NOVO (4 transições simples verificam notificação)
  OrdensServico/IniciarExecucaoUseCaseTestes.cs                 MODIFICADO (+ Mock<INotificacaoGateway>; verifica notificação)
tests/Oficina.Adaptadores.Testes/
  OrdensServico/OrdemDeServicoControllerTestes.cs               MODIFICADO (helper CriarController passa Mock<INotificacaoGateway>)

# ---- Task 2: Listagem ordenada ----
src/Oficina.Adaptadores/
  OrdensServico/DataSources/OrdemDeServicoQuery.cs              ★NOVO (helper puro: AplicarFiltro + AplicarOrdenacao)
src/Oficina.Infraestrutura/
  Persistencia/DataSources/OrdemDeServicoDataSource.cs          MODIFICADO (ListarAsync/ContarAsync usam o helper)
tests/Oficina.Adaptadores.Testes/
  OrdensServico/OrdemDeServicoQueryTestes.cs                    ★NOVO (filtro + ordenação por prioridade, em memória)

# ---- Task 3: Abertura de OS consolidada ----
src/Oficina.Aplicacao/
  OrdensServico/Dtos/AbrirOrdemRequest.cs                       ★NOVO (records aninhados)
  OrdensServico/AbrirOrdemDeServicoUseCase.cs                   ★NOVO (fluxo atômico find-or-create)
  OrdensServico/Validacoes/AbrirOrdemValidator.cs               ★NOVO (FluentValidation)
  DependencyInjectionAplicacao.cs                               MODIFICADO (registra AbrirOrdemDeServicoUseCase)
src/Oficina.Adaptadores/
  OrdensServico/Controllers/OrdemDeServicoController.cs         MODIFICADO (+ AbrirAsync + ctor)
src/Oficina.Api/
  Controllers/OrdensServicoController.cs                        MODIFICADO (POST recebe AbrirOrdemRequest → AbrirAsync)
tests/Oficina.Aplicacao.Testes/
  OrdensServico/AbrirOrdemDeServicoUseCaseTestes.cs             ★NOVO (cliente novo/existente, veículo existente, serviço/peça inválidos)
tests/Oficina.Adaptadores.Testes/
  OrdensServico/OrdemDeServicoControllerTestes.cs               MODIFICADO (CriarController + teste de AbrirAsync)
tests/Oficina.Integracao.Testes/                               MODIFICADO (CI): OS criada via AbrirOrdemRequest
  OrdensServico/OrdensServicoFluxoTestes.cs
  Consulta/ConsultaPublicaTestes.cs

# ---- Task 4: Webhook de aprovação externa ----
src/Oficina.Aplicacao/
  OrdensServico/RegistrarDecisaoDeOrcamentoUseCase.cs           ★NOVO
  OrdensServico/Dtos/DecisaoOrcamentoRequest.cs                 ★NOVO
  Consulta/AprovarOrcamentoPorClienteUseCase.cs                 DELETADO
  Consulta/RejeitarOrcamentoPorClienteUseCase.cs                DELETADO
  DependencyInjectionAplicacao.cs                               MODIFICADO (remove Aprovar/Rejeitar; add RegistrarDecisao)
src/Oficina.Adaptadores/
  OrdensServico/Webhooks/ValidadorTokenWebhook.cs              ★NOVO (comparação fixed-time, fail-closed)
  OrdensServico/Controllers/OrdemDeServicoController.cs         MODIFICADO (+ RegistrarDecisaoDeOrcamentoAsync + ctor)
src/Oficina.Api/
  Configuracao/ConfiguracaoWebhook.cs                           ★NOVO (WebhookOptions + AdicionarWebhook)
  Controllers/WebhooksController.cs                             ★NOVO ([AllowAnonymous]; header X-Webhook-Token)
  Controllers/ConsultaController.cs                             MODIFICADO (remove Aprovar/Rejeitar; mantém GET + anti-enum)
  Program.cs                                                    MODIFICADO (AdicionarWebhook)
  appsettings.json                                             MODIFICADO (chave Webhook:Token)
.env.example                                                   MODIFICADO (WEBHOOK_TOKEN)
docker/docker-compose.yml                                      MODIFICADO (Webhook__Token — documentação)
tests/Oficina.Aplicacao.Testes/
  OrdensServico/RegistrarDecisaoDeOrcamentoUseCaseTestes.cs     ★NOVO
  Consulta/AprovarOrcamentoPorClienteUseCaseTestes.cs          DELETADO
tests/Oficina.Adaptadores.Testes/
  OrdensServico/ValidadorTokenWebhookTestes.cs                  ★NOVO
  OrdensServico/OrdemDeServicoControllerTestes.cs              MODIFICADO (CriarController + teste de decisão)
tests/Oficina.Integracao.Testes/                               MODIFICADO (CI)
  Consulta/ConsultaPublicaTestes.cs                            (remove aprovar/rejeitar; add webhook)
  Auth/AuthFixture.cs                                          (env Webhook__Token)
```

**Resumo:** 4 Tasks. Task 1: cria **4**, modifica **8**. Task 2: cria **2**, modifica **1**. Task 3: cria **4**, modifica **6** (2 são integração/CI). Task 4: cria **7**, modifica **8** (2 integração/CI), deleta **3**. `OrdemDeServicoControllerTestes.cs` é tocado nas Tasks 1, 3 e 4 (o helper `CriarController` cresce).

---

## Task 1: Notificação de status por e-mail (mock)

Adiciona a porta `INotificacaoGateway` (camada de Casos de Uso), uma impl mock em Infraestrutura que **loga** a mudança de status, e faz as 5 transições de OS notificarem **após** persistir. O contrato HTTP não muda. Ao final: **build + 3 projetos de teste unitário verdes**.

**Files:**
- Create: `src/Oficina.Aplicacao/OrdensServico/Gateways/INotificacaoGateway.cs`
- Create: `src/Oficina.Infraestrutura/Notificacoes/NotificacaoEmailMock.cs`
- Create: `src/Oficina.Infraestrutura/Notificacoes/DependencyInjectionNotificacoes.cs`
- Modify: `src/Oficina.Infraestrutura/DependencyInjection.cs`
- Modify: `src/Oficina.Aplicacao/OrdensServico/IniciarDiagnosticoUseCase.cs`
- Modify: `src/Oficina.Aplicacao/OrdensServico/EnviarOrcamentoParaAprovacaoUseCase.cs`
- Modify: `src/Oficina.Aplicacao/OrdensServico/IniciarExecucaoUseCase.cs`
- Modify: `src/Oficina.Aplicacao/OrdensServico/FinalizarOrdemUseCase.cs`
- Modify: `src/Oficina.Aplicacao/OrdensServico/EntregarOrdemUseCase.cs`
- Create: `tests/Oficina.Aplicacao.Testes/OrdensServico/NotificacaoTransicaoUseCaseTestes.cs`
- Modify: `tests/Oficina.Aplicacao.Testes/OrdensServico/IniciarExecucaoUseCaseTestes.cs`
- Modify: `tests/Oficina.Adaptadores.Testes/OrdensServico/OrdemDeServicoControllerTestes.cs`

**Interfaces:**
- Produces: `Oficina.Aplicacao.OrdensServico.Gateways.INotificacaoGateway` com `Task NotificarMudancaDeStatusAsync(OrdemDeServico ordem, CancellationToken ct)`.
- `NotificacaoEmailMock : INotificacaoGateway` loga via `ILogger<NotificacaoEmailMock>`.
- Assinaturas públicas dos 5 use cases **não mudam** (mesmo `ExecutarAsync`); só ganham a dependência `INotificacaoGateway` no construtor. O `OrdemDeServicoController` (Adaptadores) recebe as instâncias via DI — seu construtor **não muda**; só o helper `CriarController` dos testes (que instancia os use cases à mão) precisa do novo mock.

---

- [ ] **Step 1: Criar a porta `INotificacaoGateway`**

Create `src/Oficina.Aplicacao/OrdensServico/Gateways/INotificacaoGateway.cs`:
```csharp
using Oficina.Dominio.OrdensServico;

namespace Oficina.Aplicacao.OrdensServico.Gateways;

// Porta de saída (Clean Architecture): notifica o cliente sobre mudanças de
// status da OS. A implementação concreta (e-mail, SMS, etc.) vive em
// Oficina.Infraestrutura. No MVP é um mock que apenas registra em log.
public interface INotificacaoGateway
{
    Task NotificarMudancaDeStatusAsync(OrdemDeServico ordem, CancellationToken ct);
}
```

- [ ] **Step 2: Criar a impl mock `NotificacaoEmailMock`**

Create `src/Oficina.Infraestrutura/Notificacoes/NotificacaoEmailMock.cs`:
```csharp
using Microsoft.Extensions.Logging;
using Oficina.Aplicacao.OrdensServico.Gateways;
using Oficina.Dominio.OrdensServico;

namespace Oficina.Infraestrutura.Notificacoes;

// Mock de notificação por e-mail: no MVP apenas registra em log estruturado.
// Substituível por uma impl real (SMTP/provedor) sem tocar nos casos de uso.
public class NotificacaoEmailMock : INotificacaoGateway
{
    private readonly ILogger<NotificacaoEmailMock> _log;

    public NotificacaoEmailMock(ILogger<NotificacaoEmailMock> log) => _log = log;

    public Task NotificarMudancaDeStatusAsync(OrdemDeServico ordem, CancellationToken ct)
    {
        _log.LogInformation(
            "[e-mail] OS #{Numero} agora esta {Status} (cliente {ClienteId})",
            ordem.Numero, ordem.Status, ordem.ClienteId);
        return Task.CompletedTask;
    }
}
```

- [ ] **Step 3: Criar o registro de DI das notificações**

Create `src/Oficina.Infraestrutura/Notificacoes/DependencyInjectionNotificacoes.cs`:
```csharp
using Microsoft.Extensions.DependencyInjection;
using Oficina.Aplicacao.OrdensServico.Gateways;

namespace Oficina.Infraestrutura.Notificacoes;

public static class DependencyInjectionNotificacoes
{
    public static IServiceCollection AdicionarNotificacoes(this IServiceCollection services)
    {
        services.AddScoped<INotificacaoGateway, NotificacaoEmailMock>();
        return services;
    }
}
```

- [ ] **Step 4: Registrar as notificações em `AdicionarInfraestrutura`**

Em `src/Oficina.Infraestrutura/DependencyInjection.cs`, adicionar o `using` e a chamada. Localizar:
```csharp
using Oficina.Infraestrutura.Auth;
using Oficina.Infraestrutura.Persistencia;
```
e substituir por:
```csharp
using Oficina.Infraestrutura.Auth;
using Oficina.Infraestrutura.Notificacoes;
using Oficina.Infraestrutura.Persistencia;
```
Depois localizar:
```csharp
        services.AdicionarAutenticacao(configuration);
        services.AdicionarRepositorios();

        return services;
```
e substituir por:
```csharp
        services.AdicionarAutenticacao(configuration);
        services.AdicionarRepositorios();
        services.AdicionarNotificacoes();

        return services;
```

- [ ] **Step 5: Injetar e notificar em `IniciarDiagnosticoUseCase`**

Substituir `src/Oficina.Aplicacao/OrdensServico/IniciarDiagnosticoUseCase.cs` inteiro por:
```csharp
using Oficina.Aplicacao.OrdensServico.Gateways;
using Oficina.Dominio.OrdensServico;

namespace Oficina.Aplicacao.OrdensServico;

public class IniciarDiagnosticoUseCase
{
    private readonly IOrdemDeServicoGateway _gateway;
    private readonly INotificacaoGateway _notificacoes;

    public IniciarDiagnosticoUseCase(IOrdemDeServicoGateway gateway, INotificacaoGateway notificacoes)
    {
        _gateway = gateway;
        _notificacoes = notificacoes;
    }

    public async Task<OrdemDeServico?> ExecutarAsync(Guid id, CancellationToken ct)
    {
        var os = await _gateway.ObterPorIdAsync(id, ct);
        if (os is null) return null;
        os.IniciarDiagnostico();
        await _gateway.SalvarAsync(ct);
        await _notificacoes.NotificarMudancaDeStatusAsync(os, ct);
        return os;
    }
}
```

- [ ] **Step 6: Injetar e notificar em `EnviarOrcamentoParaAprovacaoUseCase`**

Substituir `src/Oficina.Aplicacao/OrdensServico/EnviarOrcamentoParaAprovacaoUseCase.cs` inteiro por:
```csharp
using Oficina.Aplicacao.OrdensServico.Gateways;
using Oficina.Dominio.OrdensServico;

namespace Oficina.Aplicacao.OrdensServico;

public class EnviarOrcamentoParaAprovacaoUseCase
{
    private readonly IOrdemDeServicoGateway _gateway;
    private readonly INotificacaoGateway _notificacoes;

    public EnviarOrcamentoParaAprovacaoUseCase(IOrdemDeServicoGateway gateway, INotificacaoGateway notificacoes)
    {
        _gateway = gateway;
        _notificacoes = notificacoes;
    }

    public async Task<OrdemDeServico?> ExecutarAsync(Guid id, CancellationToken ct)
    {
        var os = await _gateway.ObterPorIdAsync(id, ct);
        if (os is null) return null;
        os.EnviarOrcamentoParaAprovacao();
        await _gateway.SalvarAsync(ct);
        await _notificacoes.NotificarMudancaDeStatusAsync(os, ct);
        return os;
    }
}
```

- [ ] **Step 7: Injetar e notificar em `IniciarExecucaoUseCase`** (notifica **após** a transação, quando houve mudança)

Substituir `src/Oficina.Aplicacao/OrdensServico/IniciarExecucaoUseCase.cs` inteiro por:
```csharp
using Oficina.Aplicacao.Estoque.Gateways;
using Oficina.Aplicacao.OrdensServico.Gateways;
using Oficina.Dominio.OrdensServico;

namespace Oficina.Aplicacao.OrdensServico;

public class IniciarExecucaoUseCase
{
    private readonly IOrdemDeServicoGateway _ordens;
    private readonly IPecaGateway _pecas;
    private readonly INotificacaoGateway _notificacoes;

    public IniciarExecucaoUseCase(
        IOrdemDeServicoGateway ordens, IPecaGateway pecas, INotificacaoGateway notificacoes)
    {
        _ordens = ordens;
        _pecas = pecas;
        _notificacoes = notificacoes;
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

        // 4) notifica fora da transação serializável (efeito colateral não deve
        //    prender a transação nem provocar rollback se o "envio" falhar)
        if (resultado is not null)
            await _notificacoes.NotificarMudancaDeStatusAsync(resultado, ct);

        return resultado;
    }
}
```

- [ ] **Step 8: Injetar e notificar em `FinalizarOrdemUseCase`**

Substituir `src/Oficina.Aplicacao/OrdensServico/FinalizarOrdemUseCase.cs` inteiro por:
```csharp
using Oficina.Aplicacao.OrdensServico.Gateways;
using Oficina.Dominio.OrdensServico;

namespace Oficina.Aplicacao.OrdensServico;

public class FinalizarOrdemUseCase
{
    private readonly IOrdemDeServicoGateway _gateway;
    private readonly INotificacaoGateway _notificacoes;

    public FinalizarOrdemUseCase(IOrdemDeServicoGateway gateway, INotificacaoGateway notificacoes)
    {
        _gateway = gateway;
        _notificacoes = notificacoes;
    }

    public async Task<OrdemDeServico?> ExecutarAsync(Guid id, CancellationToken ct)
    {
        var os = await _gateway.ObterPorIdAsync(id, ct);
        if (os is null) return null;
        os.Finalizar();
        await _gateway.SalvarAsync(ct);
        await _notificacoes.NotificarMudancaDeStatusAsync(os, ct);
        return os;
    }
}
```

- [ ] **Step 9: Injetar e notificar em `EntregarOrdemUseCase`**

Substituir `src/Oficina.Aplicacao/OrdensServico/EntregarOrdemUseCase.cs` inteiro por:
```csharp
using Oficina.Aplicacao.OrdensServico.Gateways;
using Oficina.Dominio.OrdensServico;

namespace Oficina.Aplicacao.OrdensServico;

public class EntregarOrdemUseCase
{
    private readonly IOrdemDeServicoGateway _gateway;
    private readonly INotificacaoGateway _notificacoes;

    public EntregarOrdemUseCase(IOrdemDeServicoGateway gateway, INotificacaoGateway notificacoes)
    {
        _gateway = gateway;
        _notificacoes = notificacoes;
    }

    public async Task<OrdemDeServico?> ExecutarAsync(Guid id, CancellationToken ct)
    {
        var os = await _gateway.ObterPorIdAsync(id, ct);
        if (os is null) return null;
        os.Entregar();
        await _gateway.SalvarAsync(ct);
        await _notificacoes.NotificarMudancaDeStatusAsync(os, ct);
        return os;
    }
}
```

- [ ] **Step 10: Atualizar `IniciarExecucaoUseCaseTestes` (novo mock + verificação da notificação)**

Substituir `tests/Oficina.Aplicacao.Testes/OrdensServico/IniciarExecucaoUseCaseTestes.cs` inteiro por (todas as construções do use case ganham `_notificacoes.Object`; o teste de sucesso verifica a chamada; os de erro/`null` verificam que **não** notifica):
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
    private readonly Mock<INotificacaoGateway> _notificacoes = new();

    private void TransacaoIdentidade()
    {
        _ordens.Setup(r => r.EmTransacaoSerializadaAsync(
            It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>()))
            .Returns<Func<CancellationToken, Task>, CancellationToken>((f, ct) => f(ct));
    }

    private IniciarExecucaoUseCase CriarUseCase() =>
        new(_ordens.Object, _pecas.Object, _notificacoes.Object);

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

        var resp = await CriarUseCase().ExecutarAsync(os.Id, default);

        resp.Should().NotBeNull();
        os.Status.Should().Be(StatusOrdemDeServico.EmExecucao);
        peca.SaldoAtual.Should().Be(7);
        _notificacoes.Verify(n => n.NotificarMudancaDeStatusAsync(os, It.IsAny<CancellationToken>()), Times.Once);
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

        var act = async () => await CriarUseCase().ExecutarAsync(os.Id, default);

        await act.Should().ThrowAsync<SaldoInsuficienteException>();
        peca.SaldoAtual.Should().Be(2);
        _notificacoes.Verify(n => n.NotificarMudancaDeStatusAsync(It.IsAny<OrdemDeServico>(), It.IsAny<CancellationToken>()), Times.Never);
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

        var act = async () => await CriarUseCase().ExecutarAsync(os.Id, default);

        await act.Should().ThrowAsync<OrcamentoNaoAprovadoException>();
        _notificacoes.Verify(n => n.NotificarMudancaDeStatusAsync(It.IsAny<OrdemDeServico>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Executar_OsInexistente_DeveRetornarNull()
    {
        _ordens.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((OrdemDeServico?)null);
        TransacaoIdentidade();

        var resp = await CriarUseCase().ExecutarAsync(Guid.NewGuid(), default);

        resp.Should().BeNull();
        _notificacoes.Verify(n => n.NotificarMudancaDeStatusAsync(It.IsAny<OrdemDeServico>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
```

- [ ] **Step 11: Criar `NotificacaoTransicaoUseCaseTestes` (as 4 transições simples notificam)**

Create `tests/Oficina.Aplicacao.Testes/OrdensServico/NotificacaoTransicaoUseCaseTestes.cs`:
```csharp
using FluentAssertions;
using Moq;
using Oficina.Aplicacao.OrdensServico;
using Oficina.Aplicacao.OrdensServico.Gateways;
using Oficina.Dominio.OrdensServico;
using Xunit;

namespace Oficina.Aplicacao.Testes.OrdensServico;

// Verifica que as transições simples (que persistem via IOrdemDeServicoGateway)
// notificam via INotificacaoGateway após SalvarAsync, e não notificam quando a OS não existe.
public class NotificacaoTransicaoUseCaseTestes
{
    private readonly Mock<IOrdemDeServicoGateway> _ordens = new();
    private readonly Mock<INotificacaoGateway> _notificacoes = new();

    private static OrdemDeServico OsRecebidaComItem()
    {
        var os = OrdemDeServico.Criar(Guid.NewGuid(), Guid.NewGuid());
        os.AdicionarItemServico(Guid.NewGuid(), "S", 10m, 1);
        return os;
    }

    [Fact]
    public async Task IniciarDiagnostico_DeveNotificar()
    {
        var os = OsRecebidaComItem();
        _ordens.Setup(r => r.ObterPorIdAsync(os.Id, It.IsAny<CancellationToken>())).ReturnsAsync(os);

        var resp = await new IniciarDiagnosticoUseCase(_ordens.Object, _notificacoes.Object)
            .ExecutarAsync(os.Id, default);

        resp.Should().NotBeNull();
        os.Status.Should().Be(StatusOrdemDeServico.EmDiagnostico);
        _notificacoes.Verify(n => n.NotificarMudancaDeStatusAsync(os, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EnviarOrcamentoParaAprovacao_DeveNotificar()
    {
        var os = OsRecebidaComItem();
        os.IniciarDiagnostico();
        _ordens.Setup(r => r.ObterPorIdAsync(os.Id, It.IsAny<CancellationToken>())).ReturnsAsync(os);

        var resp = await new EnviarOrcamentoParaAprovacaoUseCase(_ordens.Object, _notificacoes.Object)
            .ExecutarAsync(os.Id, default);

        resp.Should().NotBeNull();
        os.Status.Should().Be(StatusOrdemDeServico.AguardandoAprovacao);
        _notificacoes.Verify(n => n.NotificarMudancaDeStatusAsync(os, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Finalizar_DeveNotificar()
    {
        var os = OsRecebidaComItem();
        os.IniciarDiagnostico();
        os.EnviarOrcamentoParaAprovacao();
        os.Aprovar();
        os.IniciarExecucao();
        _ordens.Setup(r => r.ObterPorIdAsync(os.Id, It.IsAny<CancellationToken>())).ReturnsAsync(os);

        var resp = await new FinalizarOrdemUseCase(_ordens.Object, _notificacoes.Object)
            .ExecutarAsync(os.Id, default);

        resp.Should().NotBeNull();
        os.Status.Should().Be(StatusOrdemDeServico.Finalizada);
        _notificacoes.Verify(n => n.NotificarMudancaDeStatusAsync(os, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Entregar_DeveNotificar()
    {
        var os = OsRecebidaComItem();
        os.IniciarDiagnostico();
        os.EnviarOrcamentoParaAprovacao();
        os.Aprovar();
        os.IniciarExecucao();
        os.Finalizar();
        _ordens.Setup(r => r.ObterPorIdAsync(os.Id, It.IsAny<CancellationToken>())).ReturnsAsync(os);

        var resp = await new EntregarOrdemUseCase(_ordens.Object, _notificacoes.Object)
            .ExecutarAsync(os.Id, default);

        resp.Should().NotBeNull();
        os.Status.Should().Be(StatusOrdemDeServico.Entregue);
        _notificacoes.Verify(n => n.NotificarMudancaDeStatusAsync(os, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task IniciarDiagnostico_OsInexistente_NaoNotifica()
    {
        _ordens.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((OrdemDeServico?)null);

        var resp = await new IniciarDiagnosticoUseCase(_ordens.Object, _notificacoes.Object)
            .ExecutarAsync(Guid.NewGuid(), default);

        resp.Should().BeNull();
        _notificacoes.Verify(n => n.NotificarMudancaDeStatusAsync(It.IsAny<OrdemDeServico>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
```

- [ ] **Step 12: Atualizar o helper `CriarController` de `OrdemDeServicoControllerTestes`**

Em `tests/Oficina.Adaptadores.Testes/OrdensServico/OrdemDeServicoControllerTestes.cs`, o helper instancia os use cases à mão — os 5 de transição agora exigem `INotificacaoGateway`. Adicionar o `using` (`Oficina.Aplicacao.OrdensServico.Gateways` já está presente) e um mock NoOp de notificação.

Localizar a assinatura e o corpo do helper:
```csharp
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
```
E substituir por (nota: um `Mock<INotificacaoGateway>` NoOp é criado dentro do helper; os métodos `async Task` de um mock sem setup retornam `Task.CompletedTask`):
```csharp
    private static OrdemDeServicoController CriarController(
        Mock<IOrdemDeServicoGateway> ordens,
        Mock<IClienteGateway> clientes,
        Mock<IServicoGateway> servicos,
        Mock<IPecaGateway> pecas)
    {
        var notificacoes = new Mock<INotificacaoGateway>();
        return new(
            new CriarOrdemUseCase(ordens.Object, clientes.Object),
            new ObterOrdemPorIdUseCase(ordens.Object),
            new ListarOrdensUseCase(ordens.Object),
            new IniciarDiagnosticoUseCase(ordens.Object, notificacoes.Object),
            new EnviarOrcamentoParaAprovacaoUseCase(ordens.Object, notificacoes.Object),
            new IniciarExecucaoUseCase(ordens.Object, pecas.Object, notificacoes.Object),
            new FinalizarOrdemUseCase(ordens.Object, notificacoes.Object),
            new EntregarOrdemUseCase(ordens.Object, notificacoes.Object),
            new AdicionarItemServicoUseCase(ordens.Object, servicos.Object),
            new RemoverItemServicoUseCase(ordens.Object),
            new AdicionarItemPecaUseCase(ordens.Object, pecas.Object),
            new RemoverItemPecaUseCase(ordens.Object),
            new ObterTempoMedioExecucaoUseCase(ordens.Object));
    }
```
> Nota: este helper será estendido novamente nas Tasks 3 e 4 (novos use cases no construtor do `OrdemDeServicoController`). Aqui só entra o mock de notificação.

- [ ] **Step 13: Compilar a solution**

Run:
```bash
cd "C:/Users/thiago/source/repos/Fiap/fiap_15SOAT_fase1"
dotnet build Oficina.sln
```
Expected: **Build succeeded**, 0 erros. (Se o build acusar `INotificacaoGateway` faltando em algum construtor, é porque um dos 5 use cases não foi atualizado — revisar Steps 5–9.)

- [ ] **Step 14: Rodar os 3 projetos de teste unitário (um por chamada)**

Run:
```bash
cd "C:/Users/thiago/source/repos/Fiap/fiap_15SOAT_fase1"
dotnet test tests/Oficina.Dominio.Testes
dotnet test tests/Oficina.Aplicacao.Testes
dotnet test tests/Oficina.Adaptadores.Testes
```
Expected: **PASS** nos três. `NotificacaoTransicaoUseCaseTestes` e `IniciarExecucaoUseCaseTestes` verificam a notificação; `Oficina.Adaptadores.Testes` continua verde com o helper atualizado.

- [ ] **Step 15: Commit**

```bash
cd "C:/Users/thiago/source/repos/Fiap/fiap_15SOAT_fase1"
git add -A
git commit -m "$(cat <<'EOF'
feat: notificar cliente por e-mail (mock) nas transicoes de status da OS

Adiciona a porta INotificacaoGateway na camada de casos de uso e uma
implementacao mock (NotificacaoEmailMock) em Oficina.Infraestrutura que
registra em log estruturado a mudanca de status. Os 5 casos de uso de
transicao (IniciarDiagnostico, EnviarOrcamentoParaAprovacao,
IniciarExecucao, Finalizar, Entregar) passam a notificar apos persistir.
Em IniciarExecucao a notificacao ocorre fora da transacao serializavel.
Nao altera o contrato HTTP.

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 2: Listagem ordenada (exclui terminais + prioridade de status)

`GET /api/v1/ordens-servico` **sem filtro de status** passa a excluir `Finalizada`/`Entregue`/`Cancelada` e a ordenar por prioridade de status (`EmExecucao`=1 > `AguardandoAprovacao`=2 > `EmDiagnostico`=3 > `Recebida`=4 > demais=5) e depois por `CriadaEm` **ascendente** (mais antigas primeiro). Com filtro de status preenchido, retorna apenas aquele status (inclui terminais). A lógica pura de filtro/ordenação é extraída para um helper estático em `Oficina.Adaptadores` (unit-testável em memória via `IQueryable`); o `OrdemDeServicoDataSource` (Infra) a consome tanto em `ListarAsync` quanto em `ContarAsync` (para o `Total` da paginação ficar coerente com a lista). Ao final: **build + 3 projetos de teste unitário verdes**.

**Files:**
- Create: `src/Oficina.Adaptadores/OrdensServico/DataSources/OrdemDeServicoQuery.cs`
- Modify: `src/Oficina.Infraestrutura/Persistencia/DataSources/OrdemDeServicoDataSource.cs`
- Create: `tests/Oficina.Adaptadores.Testes/OrdensServico/OrdemDeServicoQueryTestes.cs`

**Interfaces:**
- `Oficina.Adaptadores.OrdensServico.DataSources.OrdemDeServicoQuery` (estático):
  - `IQueryable<OrdemDeServico> AplicarFiltro(IQueryable<OrdemDeServico> query, StatusOrdemDeServico? statusFiltro)` — com filtro: `Where(Status == filtro)`; sem filtro: exclui os 3 terminais.
  - `IQueryable<OrdemDeServico> AplicarOrdenacao(IQueryable<OrdemDeServico> query)` — `OrderBy(prioridade)` (expressão condicional traduzível para `CASE`) `ThenBy(CriadaEm)`.
- `IOrdemDeServicoGateway`/`IOrdemDeServicoDataSource` **não mudam** de assinatura — só a implementação EF de `ListarAsync`/`ContarAsync`.

---

- [ ] **Step 1: Criar o helper puro `OrdemDeServicoQuery`**

Create `src/Oficina.Adaptadores/OrdensServico/DataSources/OrdemDeServicoQuery.cs`:
```csharp
using Oficina.Dominio.OrdensServico;

namespace Oficina.Adaptadores.OrdensServico.DataSources;

// Regras de listagem de OS extraídas para expressões puras sobre IQueryable,
// traduzíveis pelo EF Core (CASE/WHERE) e testáveis em memória (LINQ-to-Objects).
public static class OrdemDeServicoQuery
{
    // Status que NÃO aparecem na listagem padrão (sem filtro).
    public static readonly IReadOnlyList<StatusOrdemDeServico> StatusTerminais = new[]
    {
        StatusOrdemDeServico.Finalizada,
        StatusOrdemDeServico.Entregue,
        StatusOrdemDeServico.Cancelada
    };

    public static IQueryable<OrdemDeServico> AplicarFiltro(
        IQueryable<OrdemDeServico> query, StatusOrdemDeServico? statusFiltro)
    {
        if (statusFiltro.HasValue)
            return query.Where(o => o.Status == statusFiltro.Value);

        // Sem filtro: esconde as OS terminais (trabalho concluído/cancelado).
        return query.Where(o =>
            o.Status != StatusOrdemDeServico.Finalizada &&
            o.Status != StatusOrdemDeServico.Entregue &&
            o.Status != StatusOrdemDeServico.Cancelada);
    }

    // Prioridade: fila de trabalho do pátio — o que está em execução primeiro,
    // depois aguardando aprovação, diagnóstico e recém-recebidas; empate por
    // data de criação ascendente (mais antigas primeiro).
    public static IQueryable<OrdemDeServico> AplicarOrdenacao(IQueryable<OrdemDeServico> query) =>
        query
            .OrderBy(o =>
                o.Status == StatusOrdemDeServico.EmExecucao ? 1 :
                o.Status == StatusOrdemDeServico.AguardandoAprovacao ? 2 :
                o.Status == StatusOrdemDeServico.EmDiagnostico ? 3 :
                o.Status == StatusOrdemDeServico.Recebida ? 4 : 5)
            .ThenBy(o => o.CriadaEm);
}
```

- [ ] **Step 2: Consumir o helper em `OrdemDeServicoDataSource`**

Em `src/Oficina.Infraestrutura/Persistencia/DataSources/OrdemDeServicoDataSource.cs` (o `using Oficina.Adaptadores.OrdensServico.DataSources;` já existe no topo), localizar os métodos `ListarAsync` e `ContarAsync`:
```csharp
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
```
E substituir por (usa `OrdemDeServicoQuery`; `ContarAsync` aplica o **mesmo** filtro para o `Total` bater com a lista):
```csharp
    public async Task<IReadOnlyList<OrdemDeServico>> ListarAsync(
        StatusOrdemDeServico? status, int pagina, int tamanhoPagina, CancellationToken ct)
    {
        if (pagina < 1) pagina = 1;
        if (tamanhoPagina < 1 || tamanhoPagina > 100) tamanhoPagina = 20;

        var q = _db.OrdensServico
            .Include(o => o.ItensServico)
            .Include(o => o.ItensPeca)
            .AsQueryable();

        q = OrdemDeServicoQuery.AplicarFiltro(q, status);
        q = OrdemDeServicoQuery.AplicarOrdenacao(q);

        return await q
            .Skip((pagina - 1) * tamanhoPagina)
            .Take(tamanhoPagina)
            .ToListAsync(ct);
    }

    public Task<int> ContarAsync(StatusOrdemDeServico? status, CancellationToken ct)
    {
        var q = OrdemDeServicoQuery.AplicarFiltro(_db.OrdensServico.AsQueryable(), status);
        return q.CountAsync(ct);
    }
```

- [ ] **Step 3: Criar `OrdemDeServicoQueryTestes` (filtro + ordenação por prioridade, em memória)**

Create `tests/Oficina.Adaptadores.Testes/OrdensServico/OrdemDeServicoQueryTestes.cs`:
```csharp
using FluentAssertions;
using Oficina.Adaptadores.OrdensServico.DataSources;
using Oficina.Dominio.OrdensServico;

namespace Oficina.Adaptadores.Testes.OrdensServico;

public class OrdemDeServicoQueryTestes
{
    // Fábricas que dirigem a OS até cada status usando apenas o domínio.
    private static OrdemDeServico Recebida()
    {
        var os = OrdemDeServico.Criar(Guid.NewGuid(), Guid.NewGuid());
        os.AdicionarItemServico(Guid.NewGuid(), "S", 10m, 1);
        return os;
    }

    private static OrdemDeServico EmDiagnostico()
    {
        var os = Recebida();
        os.IniciarDiagnostico();
        return os;
    }

    private static OrdemDeServico AguardandoAprovacao()
    {
        var os = EmDiagnostico();
        os.EnviarOrcamentoParaAprovacao();
        return os;
    }

    private static OrdemDeServico EmExecucao()
    {
        var os = AguardandoAprovacao();
        os.Aprovar();
        os.IniciarExecucao();
        return os;
    }

    private static OrdemDeServico Finalizada()
    {
        var os = EmExecucao();
        os.Finalizar();
        return os;
    }

    private static OrdemDeServico Entregue()
    {
        var os = Finalizada();
        os.Entregar();
        return os;
    }

    private static OrdemDeServico Cancelada()
    {
        var os = AguardandoAprovacao();
        os.Rejeitar();
        return os;
    }

    [Fact]
    public void AplicarFiltro_SemFiltro_ExcluiTerminais()
    {
        var fonte = new[]
        {
            Recebida(), EmDiagnostico(), AguardandoAprovacao(), EmExecucao(),
            Finalizada(), Entregue(), Cancelada()
        }.AsQueryable();

        var resultado = OrdemDeServicoQuery.AplicarFiltro(fonte, null).ToList();

        resultado.Should().OnlyContain(o =>
            o.Status != StatusOrdemDeServico.Finalizada &&
            o.Status != StatusOrdemDeServico.Entregue &&
            o.Status != StatusOrdemDeServico.Cancelada);
        resultado.Should().HaveCount(4);
    }

    [Fact]
    public void AplicarFiltro_ComStatusTerminal_IncluiApenasEle()
    {
        var fonte = new[] { Recebida(), Entregue(), Cancelada() }.AsQueryable();

        var resultado = OrdemDeServicoQuery
            .AplicarFiltro(fonte, StatusOrdemDeServico.Entregue).ToList();

        resultado.Should().ContainSingle()
            .Which.Status.Should().Be(StatusOrdemDeServico.Entregue);
    }

    [Fact]
    public void AplicarOrdenacao_OrdenaPorPrioridadeDeStatus()
    {
        // fonte propositalmente fora de ordem
        var fonte = new[]
        {
            Recebida(), EmExecucao(), EmDiagnostico(), AguardandoAprovacao()
        }.AsQueryable();

        var ordenado = OrdemDeServicoQuery.AplicarOrdenacao(fonte)
            .Select(o => o.Status).ToList();

        ordenado.Should().Equal(
            StatusOrdemDeServico.EmExecucao,
            StatusOrdemDeServico.AguardandoAprovacao,
            StatusOrdemDeServico.EmDiagnostico,
            StatusOrdemDeServico.Recebida);
    }

    [Fact]
    public void FiltroMaisOrdenacao_SemFiltro_ExcluiTerminaisEOrdena()
    {
        var fonte = new[]
        {
            Entregue(), Recebida(), Cancelada(), EmExecucao(),
            Finalizada(), EmDiagnostico(), AguardandoAprovacao()
        }.AsQueryable();

        var filtrado = OrdemDeServicoQuery.AplicarFiltro(fonte, null);
        var resultado = OrdemDeServicoQuery.AplicarOrdenacao(filtrado)
            .Select(o => o.Status).ToList();

        resultado.Should().Equal(
            StatusOrdemDeServico.EmExecucao,
            StatusOrdemDeServico.AguardandoAprovacao,
            StatusOrdemDeServico.EmDiagnostico,
            StatusOrdemDeServico.Recebida);
    }
}
```
> **Cobertura do desempate por `CriadaEm`:** o `ThenBy(o => o.CriadaEm)` é um critério secundário sobre um timestamp real (`DateTimeOffset.UtcNow`, `private set`, não injetável no domínio). Como a ordenação primária por prioridade é totalmente determinística e coberta acima, o desempate por `CriadaEm` (parte da mesma expressão `AplicarOrdenacao`) é verificado **fim-a-fim no CI** pelos testes de integração de listagem — não é reintroduzido flakiness com `Thread.Sleep` aqui.

- [ ] **Step 4: Compilar a solution**

Run:
```bash
cd "C:/Users/thiago/source/repos/Fiap/fiap_15SOAT_fase1"
dotnet build Oficina.sln
```
Expected: **Build succeeded**, 0 erros.

- [ ] **Step 5: Rodar os 3 projetos de teste unitário (um por chamada)**

Run:
```bash
cd "C:/Users/thiago/source/repos/Fiap/fiap_15SOAT_fase1"
dotnet test tests/Oficina.Dominio.Testes
dotnet test tests/Oficina.Aplicacao.Testes
dotnet test tests/Oficina.Adaptadores.Testes
```
Expected: **PASS** nos três. `OrdemDeServicoQueryTestes` cobre filtro e ordenação por prioridade.

- [ ] **Step 6: Commit**

```bash
cd "C:/Users/thiago/source/repos/Fiap/fiap_15SOAT_fase1"
git add -A
git commit -m "$(cat <<'EOF'
feat: listagem de OS exclui terminais e ordena por prioridade de status

GET /api/v1/ordens-servico sem filtro passa a esconder OS Finalizada,
Entregue e Cancelada e a ordenar por prioridade (EmExecucao,
AguardandoAprovacao, EmDiagnostico, Recebida) e depois por CriadaEm
ascendente. Com filtro de status preenchido, retorna apenas aquele status
(inclui terminais). A logica de filtro/ordenacao vira expressoes puras em
OrdemDeServicoQuery (Adaptadores), traduziveis pelo EF e testaveis em
memoria; ContarAsync aplica o mesmo filtro para o Total bater com a lista.

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 3: Abertura de OS consolidada

`POST /api/v1/ordens-servico` passa a receber `AbrirOrdemRequest` (dados do cliente + veículo + listas de serviços e peças) e a abrir a OS **atomicamente**: find-or-create do cliente (por documento) e do veículo (por placa), criação da OS e inclusão dos itens, tudo num **único** `SalvarAsync` (os gateways compartilham o `OficinaDbContext` scoped). Recarrega por Id para popular `Numero` (BIGSERIAL) e retorna `OrdemResponse`. Reaproveita as validações de domínio (documento/placa/preço/quantidade). Ao final: **build + 3 projetos de teste unitário verdes**.

**Decisão de design (registrada):** o antigo `CriarOrdemUseCase` (abertura por ids), seu `CriarOrdemRequest`, o `CriarOrdemValidator` e o método `OrdemDeServicoController.CriarAsync` **são mantidos** como uso interno (sem endpoint HTTP), para minimizar churn e preservar os testes já verdes. Apenas o **endpoint** `POST` deixa de usá-los, passando a delegar ao novo `AbrirAsync`.

**Files:**
- Create: `src/Oficina.Aplicacao/OrdensServico/Dtos/AbrirOrdemRequest.cs`
- Create: `src/Oficina.Aplicacao/OrdensServico/AbrirOrdemDeServicoUseCase.cs`
- Create: `src/Oficina.Aplicacao/OrdensServico/Validacoes/AbrirOrdemValidator.cs`
- Modify: `src/Oficina.Aplicacao/DependencyInjectionAplicacao.cs`
- Modify: `src/Oficina.Adaptadores/OrdensServico/Controllers/OrdemDeServicoController.cs`
- Modify: `src/Oficina.Api/Controllers/OrdensServicoController.cs`
- Create: `tests/Oficina.Aplicacao.Testes/OrdensServico/AbrirOrdemDeServicoUseCaseTestes.cs`
- Modify: `tests/Oficina.Adaptadores.Testes/OrdensServico/OrdemDeServicoControllerTestes.cs`
- Modify (CI): `tests/Oficina.Integracao.Testes/OrdensServico/OrdensServicoFluxoTestes.cs`
- Modify (CI): `tests/Oficina.Integracao.Testes/Consulta/ConsultaPublicaTestes.cs`

**Interfaces:**
- `Oficina.Aplicacao.OrdensServico.Dtos.AbrirOrdemRequest` (records aninhados).
- `Oficina.Aplicacao.OrdensServico.AbrirOrdemDeServicoUseCase.ExecutarAsync(AbrirOrdemRequest, CancellationToken) : Task<OrdemDeServico>` (ctor: `IClienteGateway`, `IServicoGateway`, `IPecaGateway`, `IOrdemDeServicoGateway`).
- `OrdemDeServicoController.AbrirAsync(AbrirOrdemRequest, CancellationToken) : Task<OrdemResponse>`.

---

- [ ] **Step 1: Criar o DTO `AbrirOrdemRequest` (records aninhados)**

Create `src/Oficina.Aplicacao/OrdensServico/Dtos/AbrirOrdemRequest.cs`:
```csharp
namespace Oficina.Aplicacao.OrdensServico.Dtos;

public sealed record AbrirOrdemRequest(
    ClienteDadosDto ClienteDados,
    VeiculoDadosDto VeiculoDados,
    IReadOnlyList<ItemServicoDto> Servicos,
    IReadOnlyList<ItemPecaDto> Pecas,
    string? Observacoes = null);

public sealed record ClienteDadosDto(string Documento, string Nome, string Email, string Telefone);

public sealed record VeiculoDadosDto(string Placa, string Marca, string Modelo, int Ano);

public sealed record ItemServicoDto(Guid ServicoId, int Quantidade);

public sealed record ItemPecaDto(Guid PecaId, int Quantidade);
```

- [ ] **Step 2: Criar o `AbrirOrdemDeServicoUseCase` (fluxo atômico find-or-create)**

Create `src/Oficina.Aplicacao/OrdensServico/AbrirOrdemDeServicoUseCase.cs`:
```csharp
using Oficina.Aplicacao.Catalogo.Gateways;
using Oficina.Aplicacao.Clientes.Gateways;
using Oficina.Aplicacao.Estoque.Gateways;
using Oficina.Aplicacao.OrdensServico.Dtos;
using Oficina.Aplicacao.OrdensServico.Gateways;
using Oficina.Dominio.Clientes;
using Oficina.Dominio.OrdensServico;

namespace Oficina.Aplicacao.OrdensServico;

// Abre uma OS de forma consolidada: cria/reaproveita cliente (por documento) e
// veículo (por placa), monta a OS com serviços/peças e persiste tudo numa única
// unidade de trabalho. Os gateways compartilham o mesmo OficinaDbContext scoped,
// então um único SalvarAsync commita cliente+veículo+OS+itens atomicamente.
public class AbrirOrdemDeServicoUseCase
{
    private readonly IClienteGateway _clientes;
    private readonly IServicoGateway _servicos;
    private readonly IPecaGateway _pecas;
    private readonly IOrdemDeServicoGateway _ordens;

    public AbrirOrdemDeServicoUseCase(
        IClienteGateway clientes,
        IServicoGateway servicos,
        IPecaGateway pecas,
        IOrdemDeServicoGateway ordens)
    {
        _clientes = clientes;
        _servicos = servicos;
        _pecas = pecas;
        _ordens = ordens;
    }

    public async Task<OrdemDeServico> ExecutarAsync(AbrirOrdemRequest req, CancellationToken ct)
    {
        // 1) find-or-create cliente por documento
        var documento = Documento.Criar(req.ClienteDados.Documento);
        var cliente = await _clientes.ObterPorDocumentoAsync(documento, ct);
        var clienteNovo = cliente is null;
        if (cliente is null)
        {
            cliente = Cliente.Criar(
                req.ClienteDados.Nome,
                documento,
                Email.Criar(req.ClienteDados.Email),
                Telefone.Criar(req.ClienteDados.Telefone));
        }

        // 2) find-or-add veículo por placa
        var placa = Placa.Criar(req.VeiculoDados.Placa);
        var veiculo = cliente.Veiculos.FirstOrDefault(v => v.Placa.Equals(placa));
        var veiculoNovo = veiculo is null;
        if (veiculo is null)
        {
            veiculo = Veiculo.Criar(placa, req.VeiculoDados.Marca, req.VeiculoDados.Modelo, req.VeiculoDados.Ano);
            cliente.AdicionarVeiculo(veiculo);
        }

        // Persistência do cliente/veículo no change tracker (sem SalvarAsync ainda):
        //  - cliente novo: AdicionarAsync insere o grafo completo (cliente + veículo);
        //  - cliente existente com veículo novo: MarcarVeiculoComoNovo marca o veículo
        //    (workaround de change detection para Id pré-setado em navigation collection).
        if (clienteNovo)
            await _clientes.AdicionarAsync(cliente, ct);
        else if (veiculoNovo)
            _clientes.MarcarVeiculoComoNovo(veiculo);

        // 3) cria a OS
        var ordem = OrdemDeServico.Criar(cliente.Id, veiculo.Id, req.Observacoes);
        await _ordens.AdicionarAsync(ordem, ct);

        // 4) serviços
        foreach (var s in req.Servicos)
        {
            var servico = await _servicos.ObterPorIdAsync(s.ServicoId, ct)
                ?? throw new OrdemInvalidaException($"Serviço {s.ServicoId} não encontrado.");
            if (!servico.Ativo)
                throw new OrdemInvalidaException($"Serviço '{servico.Nome}' está inativo.");

            var item = ordem.AdicionarItemServico(servico.Id, servico.Nome, servico.PrecoBase, s.Quantidade);
            _ordens.MarcarItemServicoComoNovo(item);
        }

        // 5) peças
        foreach (var p in req.Pecas)
        {
            var peca = await _pecas.ObterPorIdAsync(p.PecaId, ct)
                ?? throw new OrdemInvalidaException($"Peça {p.PecaId} não encontrada.");
            if (!peca.Ativo)
                throw new OrdemInvalidaException($"Peça '{peca.Nome}' está inativa.");

            var item = ordem.AdicionarItemPeca(peca.Id, peca.Nome, peca.PrecoUnitario, p.Quantidade);
            _ordens.MarcarItemPecaComoNovo(item);
        }

        // 6) única unidade de trabalho — commita cliente + veículo + OS + itens
        await _ordens.SalvarAsync(ct);

        // 7) recarrega para popular Numero (BIGSERIAL preenchido pelo banco)
        return await _ordens.ObterPorIdAsync(ordem.Id, ct)
            ?? throw new InvalidOperationException("OS não encontrada após abertura.");
    }
}
```
> **Nota sobre atomicidade / `MarcarVeiculoComoNovo`:** o enunciado descreve `MarcarVeiculoComoNovo` no passo do veículo. Aqui só o chamamos quando o **cliente já existia** (entidade rastreada) e o veículo é novo — que é exatamente o caso do workaround de change tracking. Para cliente **novo**, `AdicionarAsync(cliente)` já insere o grafo inteiro (cliente + veículo), então marcar o veículo de novo seria redundante e potencialmente incorreto (o veículo já é `Added` via grafo). Isso preserva a atomicidade (um único `SaveChanges`).

- [ ] **Step 3: Criar o `AbrirOrdemValidator` (FluentValidation)**

Create `src/Oficina.Aplicacao/OrdensServico/Validacoes/AbrirOrdemValidator.cs`:
```csharp
using FluentValidation;
using Oficina.Aplicacao.OrdensServico.Dtos;

namespace Oficina.Aplicacao.OrdensServico.Validacoes;

public class AbrirOrdemValidator : AbstractValidator<AbrirOrdemRequest>
{
    public AbrirOrdemValidator()
    {
        RuleFor(x => x.ClienteDados).NotNull().SetValidator(new ClienteDadosValidator());
        RuleFor(x => x.VeiculoDados).NotNull().SetValidator(new VeiculoDadosValidator());
        RuleFor(x => x.Observacoes).MaximumLength(2000);

        RuleForEach(x => x.Servicos).SetValidator(new ItemServicoDtoValidator());
        RuleForEach(x => x.Pecas).SetValidator(new ItemPecaDtoValidator());

        // Uma OS aberta deve ter pelo menos um item (serviço ou peça).
        RuleFor(x => x)
            .Must(x => (x.Servicos?.Count ?? 0) + (x.Pecas?.Count ?? 0) > 0)
            .WithMessage("Informe ao menos um serviço ou peça para abrir a ordem.");
    }
}

public class ClienteDadosValidator : AbstractValidator<ClienteDadosDto>
{
    public ClienteDadosValidator()
    {
        RuleFor(x => x.Documento).NotEmpty();
        RuleFor(x => x.Nome).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Email).NotEmpty();
        RuleFor(x => x.Telefone).NotEmpty();
    }
}

public class VeiculoDadosValidator : AbstractValidator<VeiculoDadosDto>
{
    public VeiculoDadosValidator()
    {
        RuleFor(x => x.Placa).NotEmpty();
        RuleFor(x => x.Marca).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Modelo).NotEmpty().MaximumLength(80);
        RuleFor(x => x.Ano).GreaterThanOrEqualTo(1900);
    }
}

public class ItemServicoDtoValidator : AbstractValidator<ItemServicoDto>
{
    public ItemServicoDtoValidator()
    {
        RuleFor(x => x.ServicoId).NotEqual(Guid.Empty);
        RuleFor(x => x.Quantidade).GreaterThan(0);
    }
}

public class ItemPecaDtoValidator : AbstractValidator<ItemPecaDto>
{
    public ItemPecaDtoValidator()
    {
        RuleFor(x => x.PecaId).NotEqual(Guid.Empty);
        RuleFor(x => x.Quantidade).GreaterThan(0);
    }
}
```
> `AddValidatorsFromAssembly` (já registrado em `DependencyInjectionAplicacao`) descobre `AbrirOrdemValidator` automaticamente; os validators aninhados são instanciados manualmente via `SetValidator(new ...)` acima, então não precisam de registro próprio.

- [ ] **Step 4: Registrar o use case no DI de Aplicação**

Em `src/Oficina.Aplicacao/DependencyInjectionAplicacao.cs`, localizar:
```csharp
        // Ordens de Serviço
        services.AddScoped<CriarOrdemUseCase>();
        services.AddScoped<ObterOrdemPorIdUseCase>();
```
E substituir por:
```csharp
        // Ordens de Serviço
        services.AddScoped<CriarOrdemUseCase>();
        services.AddScoped<AbrirOrdemDeServicoUseCase>();
        services.AddScoped<ObterOrdemPorIdUseCase>();
```

- [ ] **Step 5: Adicionar `AbrirAsync` ao `OrdemDeServicoController` (Adaptadores)**

Em `src/Oficina.Adaptadores/OrdensServico/Controllers/OrdemDeServicoController.cs`:

(a) Adicionar o campo — localizar:
```csharp
    private readonly CriarOrdemUseCase _criar;
    private readonly ObterOrdemPorIdUseCase _obter;
```
e substituir por:
```csharp
    private readonly CriarOrdemUseCase _criar;
    private readonly AbrirOrdemDeServicoUseCase _abrir;
    private readonly ObterOrdemPorIdUseCase _obter;
```

(b) Adicionar o parâmetro do construtor — localizar:
```csharp
    public OrdemDeServicoController(
        CriarOrdemUseCase criar,
        ObterOrdemPorIdUseCase obter,
```
e substituir por:
```csharp
    public OrdemDeServicoController(
        CriarOrdemUseCase criar,
        AbrirOrdemDeServicoUseCase abrir,
        ObterOrdemPorIdUseCase obter,
```

(c) Atribuir no corpo do construtor — localizar:
```csharp
        _criar = criar;
        _obter = obter;
```
e substituir por:
```csharp
        _criar = criar;
        _abrir = abrir;
        _obter = obter;
```

(d) Adicionar o método logo após `CriarAsync` — localizar:
```csharp
    public async Task<OrdemResponse> CriarAsync(CriarOrdemRequest req, CancellationToken ct)
    {
        var ordem = await _criar.ExecutarAsync(req, ct);
        return OrdemDeServicoPresenter.Apresentar(ordem);
    }
```
e substituir por:
```csharp
    public async Task<OrdemResponse> CriarAsync(CriarOrdemRequest req, CancellationToken ct)
    {
        var ordem = await _criar.ExecutarAsync(req, ct);
        return OrdemDeServicoPresenter.Apresentar(ordem);
    }

    public async Task<OrdemResponse> AbrirAsync(AbrirOrdemRequest req, CancellationToken ct)
    {
        var ordem = await _abrir.ExecutarAsync(req, ct);
        return OrdemDeServicoPresenter.Apresentar(ordem);
    }
```

- [ ] **Step 6: Alterar o endpoint `POST` HTTP para `AbrirOrdemRequest`**

Em `src/Oficina.Api/Controllers/OrdensServicoController.cs`, localizar:
```csharp
    [HttpPost]
    public async Task<IActionResult> Criar(
        [FromBody] CriarOrdemRequest req,
        [FromServices] OrdemDeServicoController controller,
        CancellationToken ct)
    {
        var resp = await controller.CriarAsync(req, ct);
        return CreatedAtAction(nameof(Obter), new { id = resp.Id }, resp);
    }
```
E substituir por (mesmo verbo/rota/status; corpo novo é o contrato):
```csharp
    [HttpPost]
    public async Task<IActionResult> Abrir(
        [FromBody] AbrirOrdemRequest req,
        [FromServices] OrdemDeServicoController controller,
        CancellationToken ct)
    {
        var resp = await controller.AbrirAsync(req, ct);
        return CreatedAtAction(nameof(Obter), new { id = resp.Id }, resp);
    }
```
> `CriarOrdemRequest` continua importado via `using Oficina.Aplicacao.OrdensServico.Dtos;` (mesmo namespace de `AbrirOrdemRequest`), sem novo `using`.

- [ ] **Step 7: Criar `AbrirOrdemDeServicoUseCaseTestes`**

Create `tests/Oficina.Aplicacao.Testes/OrdensServico/AbrirOrdemDeServicoUseCaseTestes.cs`:
```csharp
using FluentAssertions;
using Moq;
using Oficina.Aplicacao.Catalogo.Gateways;
using Oficina.Aplicacao.Clientes.Gateways;
using Oficina.Aplicacao.Estoque.Gateways;
using Oficina.Aplicacao.OrdensServico;
using Oficina.Aplicacao.OrdensServico.Dtos;
using Oficina.Aplicacao.OrdensServico.Gateways;
using Oficina.Dominio.Catalogo;
using Oficina.Dominio.Clientes;
using Oficina.Dominio.Estoque;
using Oficina.Dominio.OrdensServico;
using Xunit;

namespace Oficina.Aplicacao.Testes.OrdensServico;

public class AbrirOrdemDeServicoUseCaseTestes
{
    private readonly Mock<IClienteGateway> _clientes = new();
    private readonly Mock<IServicoGateway> _servicos = new();
    private readonly Mock<IPecaGateway> _pecas = new();
    private readonly Mock<IOrdemDeServicoGateway> _ordens = new();

    private AbrirOrdemDeServicoUseCase CriarUseCase() =>
        new(_clientes.Object, _servicos.Object, _pecas.Object, _ordens.Object);

    // Simula o re-fetch final: devolve a mesma OS que foi adicionada.
    private void ConfigurarRefetch()
    {
        OrdemDeServico? adicionada = null;
        _ordens.Setup(o => o.AdicionarAsync(It.IsAny<OrdemDeServico>(), It.IsAny<CancellationToken>()))
            .Callback<OrdemDeServico, CancellationToken>((o, _) => adicionada = o)
            .Returns(Task.CompletedTask);
        _ordens.Setup(o => o.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => adicionada);
    }

    private static AbrirOrdemRequest RequestValido(Guid servicoId) => new(
        new ClienteDadosDto("39053344705", "João", "joao@x.com", "11987654321"),
        new VeiculoDadosDto("ABC1234", "Fiat", "Uno", 2020),
        new[] { new ItemServicoDto(servicoId, 2) },
        Array.Empty<ItemPecaDto>());

    [Fact]
    public async Task Executar_ClienteNovo_DeveCriarClienteVeiculoEOrdem()
    {
        var servico = Servico.Criar("Troca de óleo", "x", 150m, 30);
        _clientes.Setup(c => c.ObterPorDocumentoAsync(It.IsAny<Documento>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Cliente?)null);
        _servicos.Setup(s => s.ObterPorIdAsync(servico.Id, It.IsAny<CancellationToken>())).ReturnsAsync(servico);
        ConfigurarRefetch();

        var ordem = await CriarUseCase().ExecutarAsync(RequestValido(servico.Id), default);

        ordem.Should().NotBeNull();
        ordem.Status.Should().Be(StatusOrdemDeServico.Recebida);
        ordem.ItensServico.Should().ContainSingle(i => i.Subtotal == 300m);
        _clientes.Verify(c => c.AdicionarAsync(It.IsAny<Cliente>(), It.IsAny<CancellationToken>()), Times.Once);
        _clientes.Verify(c => c.MarcarVeiculoComoNovo(It.IsAny<Veiculo>()), Times.Never);
        _ordens.Verify(o => o.SalvarAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Executar_ClienteExistenteVeiculoNovo_DeveMarcarVeiculoComoNovo()
    {
        var cliente = Cliente.Criar("João", Documento.Criar("39053344705"),
            Email.Criar("joao@x.com"), Telefone.Criar("11987654321"));
        var servico = Servico.Criar("Troca de óleo", "x", 150m, 30);
        _clientes.Setup(c => c.ObterPorDocumentoAsync(It.IsAny<Documento>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(cliente);
        _servicos.Setup(s => s.ObterPorIdAsync(servico.Id, It.IsAny<CancellationToken>())).ReturnsAsync(servico);
        ConfigurarRefetch();

        var ordem = await CriarUseCase().ExecutarAsync(RequestValido(servico.Id), default);

        ordem.ClienteId.Should().Be(cliente.Id);
        cliente.Veiculos.Should().ContainSingle(v => v.Placa.Valor == "ABC1234");
        _clientes.Verify(c => c.AdicionarAsync(It.IsAny<Cliente>(), It.IsAny<CancellationToken>()), Times.Never);
        _clientes.Verify(c => c.MarcarVeiculoComoNovo(It.IsAny<Veiculo>()), Times.Once);
    }

    [Fact]
    public async Task Executar_ClienteExistenteVeiculoExistente_NaoDuplicaVeiculo()
    {
        var cliente = Cliente.Criar("João", Documento.Criar("39053344705"),
            Email.Criar("joao@x.com"), Telefone.Criar("11987654321"));
        cliente.AdicionarVeiculo(Veiculo.Criar(Placa.Criar("ABC1234"), "Fiat", "Uno", 2020));
        var servico = Servico.Criar("Troca de óleo", "x", 150m, 30);
        _clientes.Setup(c => c.ObterPorDocumentoAsync(It.IsAny<Documento>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(cliente);
        _servicos.Setup(s => s.ObterPorIdAsync(servico.Id, It.IsAny<CancellationToken>())).ReturnsAsync(servico);
        ConfigurarRefetch();

        await CriarUseCase().ExecutarAsync(RequestValido(servico.Id), default);

        cliente.Veiculos.Should().HaveCount(1);
        _clientes.Verify(c => c.MarcarVeiculoComoNovo(It.IsAny<Veiculo>()), Times.Never);
    }

    [Fact]
    public async Task Executar_ServicoInativo_DeveLancar()
    {
        var servico = Servico.Criar("Troca de óleo", "x", 150m, 30);
        servico.Inativar();
        _clientes.Setup(c => c.ObterPorDocumentoAsync(It.IsAny<Documento>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Cliente?)null);
        _servicos.Setup(s => s.ObterPorIdAsync(servico.Id, It.IsAny<CancellationToken>())).ReturnsAsync(servico);

        var act = async () => await CriarUseCase().ExecutarAsync(RequestValido(servico.Id), default);

        await act.Should().ThrowAsync<OrdemInvalidaException>().WithMessage("*inativo*");
        _ordens.Verify(o => o.SalvarAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Executar_PecaInexistente_DeveLancar()
    {
        var pecaId = Guid.NewGuid();
        _clientes.Setup(c => c.ObterPorDocumentoAsync(It.IsAny<Documento>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Cliente?)null);
        _pecas.Setup(p => p.ObterPorIdAsync(pecaId, It.IsAny<CancellationToken>())).ReturnsAsync((Peca?)null);

        var req = new AbrirOrdemRequest(
            new ClienteDadosDto("39053344705", "João", "joao@x.com", "11987654321"),
            new VeiculoDadosDto("ABC1234", "Fiat", "Uno", 2020),
            Array.Empty<ItemServicoDto>(),
            new[] { new ItemPecaDto(pecaId, 1) });

        var act = async () => await CriarUseCase().ExecutarAsync(req, default);

        await act.Should().ThrowAsync<OrdemInvalidaException>().WithMessage("*não encontrada*");
        _ordens.Verify(o => o.SalvarAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
```

- [ ] **Step 8: Estender o `CriarController` e adicionar teste de `AbrirAsync`**

Em `tests/Oficina.Adaptadores.Testes/OrdensServico/OrdemDeServicoControllerTestes.cs`:

(a) O construtor de `OrdemDeServicoController` ganhou `AbrirOrdemDeServicoUseCase` (2ª posição). Atualizar o helper — localizar (já com o mock de notificação da Task 1):
```csharp
        var notificacoes = new Mock<INotificacaoGateway>();
        return new(
            new CriarOrdemUseCase(ordens.Object, clientes.Object),
            new ObterOrdemPorIdUseCase(ordens.Object),
```
e substituir por:
```csharp
        var notificacoes = new Mock<INotificacaoGateway>();
        return new(
            new CriarOrdemUseCase(ordens.Object, clientes.Object),
            new AbrirOrdemDeServicoUseCase(clientes.Object, servicos.Object, pecas.Object, ordens.Object),
            new ObterOrdemPorIdUseCase(ordens.Object),
```

(b) Adicionar um teste de `AbrirAsync` ao final da classe (antes da chave de fechamento). Inserir:
```csharp
    [Fact]
    public async Task AbrirAsync_ClienteNovo_DeveRetornarOrdemResponseFormatada()
    {
        var ordens = new Mock<IOrdemDeServicoGateway>();
        var clientes = new Mock<IClienteGateway>();
        var servicos = new Mock<IServicoGateway>();
        var pecas = new Mock<IPecaGateway>();

        var servico = Servico.Criar("Troca de óleo", "x", 150m, 30);
        clientes.Setup(c => c.ObterPorDocumentoAsync(It.IsAny<Documento>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Cliente?)null);
        servicos.Setup(s => s.ObterPorIdAsync(servico.Id, It.IsAny<CancellationToken>())).ReturnsAsync(servico);

        OrdemDeServico? adicionada = null;
        ordens.Setup(o => o.AdicionarAsync(It.IsAny<OrdemDeServico>(), It.IsAny<CancellationToken>()))
            .Callback<OrdemDeServico, CancellationToken>((o, _) => adicionada = o)
            .Returns(Task.CompletedTask);
        ordens.Setup(o => o.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => adicionada);

        var controller = CriarController(ordens, clientes, servicos, pecas);
        var req = new AbrirOrdemRequest(
            new ClienteDadosDto("39053344705", "João", "joao@x.com", "11987654321"),
            new VeiculoDadosDto("ABC1234", "Fiat", "Uno", 2020),
            new[] { new ItemServicoDto(servico.Id, 1) },
            Array.Empty<ItemPecaDto>());

        var resp = await controller.AbrirAsync(req, default);

        resp.Should().BeOfType<OrdemResponse>();
        resp.Status.Should().Be("Recebida");
        resp.ItensServico.Should().ContainSingle(i => i.Nome == "Troca de óleo");
        clientes.Verify(c => c.AdicionarAsync(It.IsAny<Cliente>(), It.IsAny<CancellationToken>()), Times.Once);
        ordens.Verify(o => o.SalvarAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
```
> Os `using` necessários (`Oficina.Aplicacao.Estoque.Gateways`, `Oficina.Dominio.Catalogo`, `Oficina.Dominio.Clientes`, `Oficina.Aplicacao.OrdensServico.Dtos`) **já estão** no topo do arquivo.

- [ ] **Step 9: Ajustar os testes de integração (CI) que criam OS via `CriarOrdemRequest`**

> **Docker indisponível localmente** — estes arquivos são compilados no build local (garante que compilam) e **executados no CI**. O endpoint mudou de contrato, então estes POSTs precisam migrar para `AbrirOrdemRequest`.

(a) Em `tests/Oficina.Integracao.Testes/OrdensServico/OrdensServicoFluxoTestes.cs`:

No teste `FluxoCompleto_DeveAvancarPelosEstadosEBaixarEstoque`, o cliente/veículo/serviço/peça já são criados antes. Substituir o bloco "Criar OS" + "Adicionar serviço" + "Adicionar peça" (o veículo/serviço/peça continuam sendo criados nos passos anteriores para termos os ids). Localizar:
```csharp
        // Criar OS
        var osResp = await http.PostAsJsonAsync("/api/v1/ordens-servico",
            new CriarOrdemRequest(cliente.Id, veiculo!.Id, "obs"));
        osResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var os = await osResp.Content.ReadFromJsonAsync<OrdemResponse>();

        // Diagnóstico
        var diag = await http.PatchAsync($"/api/v1/ordens-servico/{os!.Id}/diagnostico", null);
        diag.StatusCode.Should().Be(HttpStatusCode.OK);

        // Adicionar serviço
        var addS = await http.PostAsJsonAsync($"/api/v1/ordens-servico/{os.Id}/servicos",
            new AdicionarItemServicoRequest(servico!.Id, 1));
        addS.StatusCode.Should().Be(HttpStatusCode.Created);

        // Adicionar peça
        var addP = await http.PostAsJsonAsync($"/api/v1/ordens-servico/{os.Id}/pecas",
            new AdicionarItemPecaRequest(peca.Id, 3));
        addP.StatusCode.Should().Be(HttpStatusCode.Created);
```
E substituir por (a abertura consolidada já inclui serviço e peça; o diagnóstico continua depois):
```csharp
        // Abrir OS consolidada (cliente/veículo find-or-create + serviço + peça)
        var osResp = await http.PostAsJsonAsync("/api/v1/ordens-servico",
            new AbrirOrdemRequest(
                new ClienteDadosDto("11144477735", "Cliente OS", $"c{Guid.NewGuid():N}@x.com", "11987654321"),
                new VeiculoDadosDto(veiculo!.Placa, "Fiat", "Uno", 2020),
                new[] { new ItemServicoDto(servico!.Id, 1) },
                new[] { new ItemPecaDto(peca.Id, 3) },
                "obs"));
        osResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var os = await osResp.Content.ReadFromJsonAsync<OrdemResponse>();

        // Diagnóstico
        var diag = await http.PatchAsync($"/api/v1/ordens-servico/{os!.Id}/diagnostico", null);
        diag.StatusCode.Should().Be(HttpStatusCode.OK);
```
> `VeiculoResponse` expõe `Placa` (string). Se o nome da propriedade diferir, usar o placa gerado no POST de veículo acima; a placa é reaproveitada pela abertura consolidada (find-or-add por placa no cliente já existente `11144477735`).

No teste `EnviarParaAprovacao_SemItens_DeveRetornar422`, a OS precisa ser aberta **sem** itens. Localizar:
```csharp
        var os = await http.PostAsJsonAsync("/api/v1/ordens-servico",
            new CriarOrdemRequest(cliente.Id, veic!.Id, null));
        var osR = await os.Content.ReadFromJsonAsync<OrdemResponse>();
        await http.PatchAsync($"/api/v1/ordens-servico/{osR!.Id}/diagnostico", null);
```
E substituir por:
```csharp
        var os = await http.PostAsJsonAsync("/api/v1/ordens-servico",
            new AbrirOrdemRequest(
                new ClienteDadosDto("11144477735", "X", $"x{Guid.NewGuid():N}@x.com", "11987654321"),
                new VeiculoDadosDto(veic!.Placa, "F", "U", 2020),
                Array.Empty<ItemServicoDto>(),
                Array.Empty<ItemPecaDto>()));
        var osR = await os.Content.ReadFromJsonAsync<OrdemResponse>();
        await http.PatchAsync($"/api/v1/ordens-servico/{osR!.Id}/diagnostico", null);
```

No teste `IniciarExecucao_SemAprovacao_DeveRetornar422`, localizar:
```csharp
        var os = await (await http.PostAsJsonAsync("/api/v1/ordens-servico",
            new CriarOrdemRequest(cli.Id, v!.Id, null))).Content.ReadFromJsonAsync<OrdemResponse>();
        await http.PatchAsync($"/api/v1/ordens-servico/{os!.Id}/diagnostico", null);
        await http.PostAsJsonAsync($"/api/v1/ordens-servico/{os.Id}/servicos",
            new AdicionarItemServicoRequest(s!.Id, 1));
        await http.PostAsync($"/api/v1/ordens-servico/{os.Id}/orcamento/enviar", null);
```
E substituir por:
```csharp
        var os = await (await http.PostAsJsonAsync("/api/v1/ordens-servico",
            new AbrirOrdemRequest(
                new ClienteDadosDto("11144477735", "Cli IE", $"ie{Guid.NewGuid():N}@x.com", "11987654321"),
                new VeiculoDadosDto(v!.Placa, "F", "U", 2020),
                new[] { new ItemServicoDto(s!.Id, 1) },
                Array.Empty<ItemPecaDto>()))).Content.ReadFromJsonAsync<OrdemResponse>();
        await http.PatchAsync($"/api/v1/ordens-servico/{os!.Id}/diagnostico", null);
        await http.PostAsync($"/api/v1/ordens-servico/{os.Id}/orcamento/enviar", null);
```

(b) Em `tests/Oficina.Integracao.Testes/Consulta/ConsultaPublicaTestes.cs`, no helper `CriarOsParaConsultaAsync`, localizar:
```csharp
        var os = await (await http.PostAsJsonAsync("/api/v1/ordens-servico",
            new CriarOrdemRequest(cli.Id, v!.Id, null))).Content.ReadFromJsonAsync<OrdemResponse>();
        await http.PatchAsync($"/api/v1/ordens-servico/{os!.Id}/diagnostico", null);

        // adicionar pelo menos um item para poder enviar para aprovação
        var s = await (await http.PostAsJsonAsync("/api/v1/servicos",
            new CriarServicoRequest("S consulta", "x", 10m, 10)))
            .Content.ReadFromJsonAsync<ServicoResponse>();
        await http.PostAsJsonAsync($"/api/v1/ordens-servico/{os.Id}/servicos",
            new AdicionarItemServicoRequest(s!.Id, 1));
        await http.PostAsync($"/api/v1/ordens-servico/{os.Id}/orcamento/enviar", null);
```
E substituir por (serviço criado antes; abertura consolidada já inclui o item):
```csharp
        var s = await (await http.PostAsJsonAsync("/api/v1/servicos",
            new CriarServicoRequest("S consulta", "x", 10m, 10)))
            .Content.ReadFromJsonAsync<ServicoResponse>();

        var os = await (await http.PostAsJsonAsync("/api/v1/ordens-servico",
            new AbrirOrdemRequest(
                new ClienteDadosDto(doc, "Cli OS", $"c{Guid.NewGuid():N}@x.com", "11987654321"),
                new VeiculoDadosDto(v!.Placa, "F", "U", 2020),
                new[] { new ItemServicoDto(s!.Id, 1) },
                Array.Empty<ItemPecaDto>()))).Content.ReadFromJsonAsync<OrdemResponse>();
        await http.PatchAsync($"/api/v1/ordens-servico/{os!.Id}/diagnostico", null);
        await http.PostAsync($"/api/v1/ordens-servico/{os.Id}/orcamento/enviar", null);
```
> Se `VeiculoResponse` não expuser `Placa`, usar a variável de placa gerada no POST do veículo (`$"OSC{...}"`). O ponto-chave é reutilizar a MESMA placa para que a abertura consolidada encontre o veículo já cadastrado no cliente `doc`.

- [ ] **Step 10: Compilar a solution**

Run:
```bash
cd "C:/Users/thiago/source/repos/Fiap/fiap_15SOAT_fase1"
dotnet build Oficina.sln
```
Expected: **Build succeeded**, 0 erros (inclui `Oficina.Integracao.Testes` compilando com os novos DTOs).

- [ ] **Step 11: Rodar os 3 projetos de teste unitário (um por chamada)**

Run:
```bash
cd "C:/Users/thiago/source/repos/Fiap/fiap_15SOAT_fase1"
dotnet test tests/Oficina.Dominio.Testes
dotnet test tests/Oficina.Aplicacao.Testes
dotnet test tests/Oficina.Adaptadores.Testes
```
Expected: **PASS** nos três. (Integração roda só no CI, com Docker.)

- [ ] **Step 12: Commit**

```bash
cd "C:/Users/thiago/source/repos/Fiap/fiap_15SOAT_fase1"
git add -A
git commit -m "$(cat <<'EOF'
feat: abertura de OS consolidada (cliente + veiculo + itens) em POST unico

POST /api/v1/ordens-servico passa a receber AbrirOrdemRequest e abre a OS
atomicamente: find-or-create do cliente (por documento) e do veiculo (por
placa), criacao da OS e inclusao de servicos/pecas, tudo num unico
SalvarAsync (os gateways compartilham o OficinaDbContext scoped). Recarrega
por Id para popular Numero. Reaproveita validacoes de dominio e adiciona
AbrirOrdemValidator. O CriarOrdemUseCase (por ids) e mantido como uso
interno, sem endpoint. Testes de integracao migrados para o novo contrato.

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 4: Webhook de aprovação externa (substitui o approve/reject público)

Cria o webhook `POST /api/v1/ordens-servico/{id:guid}/orcamento/aprovacao` (`[AllowAnonymous]`), protegido por token no header `X-Webhook-Token` comparado ao config `Webhook:Token` (env `WEBHOOK_TOKEN`). Novo `RegistrarDecisaoDeOrcamentoUseCase(Guid, bool)` aplica `Aprovar()`/`Rejeitar()`, persiste, notifica (Task 1) e retorna a entidade (ou `null` → 404). **Remove** as rotas públicas `POST /api/v1/consulta/{numeroOs}/aprovar` e `/rejeitar` e seus dois use cases; mantém `GET /api/v1/consulta/{numeroOs}` e a anti-enumeração. Ao final: **build + 3 projetos de teste unitário verdes**.

**Decisões de design (registradas):**
- **Validação de token em duas partes:** a comparação pura (fixed-time, fail-closed) fica em `ValidadorTokenWebhook` (Adaptadores, unit-testável); o controller HTTP (`WebhooksController`, Api) lê o header + `IOptions<WebhookOptions>` e retorna 401. Assim a lógica de segurança é testável sem Docker.
- **Decisão inválida → 422** (via `UnprocessableEntity` no controller, após passar pelo token), preferido a 400 para diferenciar "payload sintaticamente ok mas valor não aceito". Token ausente/errado é checado **antes** da decisão (401 tem precedência).
- **`ResultadoConsulta` inalterado:** as três variantes (`Sucesso`/`NaoEncontrada`/`DocumentoNaoConfere`) continuam usadas por `ConsultarOrdemPorNumeroUseCase` — nada vira código morto.

**Files:**
- Create: `src/Oficina.Aplicacao/OrdensServico/RegistrarDecisaoDeOrcamentoUseCase.cs`
- Create: `src/Oficina.Aplicacao/OrdensServico/Dtos/DecisaoOrcamentoRequest.cs`
- Create: `src/Oficina.Adaptadores/OrdensServico/Webhooks/ValidadorTokenWebhook.cs`
- Create: `src/Oficina.Api/Configuracao/ConfiguracaoWebhook.cs`
- Create: `src/Oficina.Api/Controllers/WebhooksController.cs`
- Modify: `src/Oficina.Adaptadores/OrdensServico/Controllers/OrdemDeServicoController.cs`
- Modify: `src/Oficina.Api/Controllers/ConsultaController.cs`
- Modify: `src/Oficina.Aplicacao/DependencyInjectionAplicacao.cs`
- Modify: `src/Oficina.Api/Program.cs`
- Modify: `src/Oficina.Api/appsettings.json`
- Modify: `.env.example`
- Modify: `docker/docker-compose.yml`
- Delete: `src/Oficina.Aplicacao/Consulta/AprovarOrcamentoPorClienteUseCase.cs`
- Delete: `src/Oficina.Aplicacao/Consulta/RejeitarOrcamentoPorClienteUseCase.cs`
- Delete: `tests/Oficina.Aplicacao.Testes/Consulta/AprovarOrcamentoPorClienteUseCaseTestes.cs`
- Create: `tests/Oficina.Aplicacao.Testes/OrdensServico/RegistrarDecisaoDeOrcamentoUseCaseTestes.cs`
- Create: `tests/Oficina.Adaptadores.Testes/OrdensServico/ValidadorTokenWebhookTestes.cs`
- Modify: `tests/Oficina.Adaptadores.Testes/OrdensServico/OrdemDeServicoControllerTestes.cs`
- Modify (CI): `tests/Oficina.Integracao.Testes/Consulta/ConsultaPublicaTestes.cs`
- Modify (CI): `tests/Oficina.Integracao.Testes/Auth/AuthFixture.cs`

**Interfaces:**
- `Oficina.Aplicacao.OrdensServico.RegistrarDecisaoDeOrcamentoUseCase.ExecutarAsync(Guid ordemId, bool aprovado, CancellationToken) : Task<OrdemDeServico?>` (ctor: `IOrdemDeServicoGateway`, `INotificacaoGateway`).
- `Oficina.Aplicacao.OrdensServico.Dtos.DecisaoOrcamentoRequest(string Decisao)`.
- `Oficina.Adaptadores.OrdensServico.Webhooks.ValidadorTokenWebhook.EhTokenValido(string? recebido, string? esperado) : bool` (estático).
- `OrdemDeServicoController.RegistrarDecisaoDeOrcamentoAsync(Guid id, bool aprovado, CancellationToken) : Task<OrdemResponse?>`.
- `Oficina.Api.Configuracao.WebhookOptions { Secao = "Webhook"; string? Token }`.

---

- [ ] **Step 1: Criar o `RegistrarDecisaoDeOrcamentoUseCase`**

Create `src/Oficina.Aplicacao/OrdensServico/RegistrarDecisaoDeOrcamentoUseCase.cs`:
```csharp
using Oficina.Aplicacao.OrdensServico.Gateways;
using Oficina.Dominio.OrdensServico;

namespace Oficina.Aplicacao.OrdensServico;

// Registra a decisão do cliente sobre o orçamento (via webhook externo).
// aprovado=true → Aprovar(); aprovado=false → Rejeitar() (cancela a OS).
// Transições inválidas lançam TransicaoDeStatusInvalidaException (→ 422 no middleware).
public class RegistrarDecisaoDeOrcamentoUseCase
{
    private readonly IOrdemDeServicoGateway _ordens;
    private readonly INotificacaoGateway _notificacoes;

    public RegistrarDecisaoDeOrcamentoUseCase(
        IOrdemDeServicoGateway ordens, INotificacaoGateway notificacoes)
    {
        _ordens = ordens;
        _notificacoes = notificacoes;
    }

    public async Task<OrdemDeServico?> ExecutarAsync(Guid ordemId, bool aprovado, CancellationToken ct)
    {
        var ordem = await _ordens.ObterPorIdAsync(ordemId, ct);
        if (ordem is null) return null;

        if (aprovado) ordem.Aprovar();
        else ordem.Rejeitar();

        await _ordens.SalvarAsync(ct);
        await _notificacoes.NotificarMudancaDeStatusAsync(ordem, ct);
        return ordem;
    }
}
```

- [ ] **Step 2: Criar o DTO `DecisaoOrcamentoRequest`**

Create `src/Oficina.Aplicacao/OrdensServico/Dtos/DecisaoOrcamentoRequest.cs`:
```csharp
namespace Oficina.Aplicacao.OrdensServico.Dtos;

// Corpo do webhook: { "decisao": "aprovado" | "recusado" }.
public sealed record DecisaoOrcamentoRequest(string Decisao);
```

- [ ] **Step 3: Criar o helper puro `ValidadorTokenWebhook`**

Create `src/Oficina.Adaptadores/OrdensServico/Webhooks/ValidadorTokenWebhook.cs`:
```csharp
using System.Security.Cryptography;
using System.Text;

namespace Oficina.Adaptadores.OrdensServico.Webhooks;

// Comparação de token do webhook em tempo constante (evita timing attack) e
// fail-closed: sem token configurado, nenhuma requisição é autorizada.
public static class ValidadorTokenWebhook
{
    public static bool EhTokenValido(string? recebido, string? esperado)
    {
        if (string.IsNullOrEmpty(esperado)) return false; // fail-closed
        if (string.IsNullOrEmpty(recebido)) return false;

        var a = Encoding.UTF8.GetBytes(recebido);
        var b = Encoding.UTF8.GetBytes(esperado);
        if (a.Length != b.Length) return false;

        return CryptographicOperations.FixedTimeEquals(a, b);
    }
}
```

- [ ] **Step 4: Criar `WebhookOptions` + extensão de DI (`ConfiguracaoWebhook`)**

Create `src/Oficina.Api/Configuracao/ConfiguracaoWebhook.cs`:
```csharp
namespace Oficina.Api.Configuracao;

public class WebhookOptions
{
    public const string Secao = "Webhook";
    public string? Token { get; set; }
}

public static class ConfiguracaoWebhook
{
    public static IServiceCollection AdicionarWebhook(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<WebhookOptions>(configuration.GetSection(WebhookOptions.Secao));
        return services;
    }
}
```

- [ ] **Step 5: Adicionar `RegistrarDecisaoDeOrcamentoAsync` ao `OrdemDeServicoController` (Adaptadores)**

Em `src/Oficina.Adaptadores/OrdensServico/Controllers/OrdemDeServicoController.cs`:

(a) Campo — localizar:
```csharp
    private readonly ObterTempoMedioExecucaoUseCase _tempoMedio;
```
e substituir por:
```csharp
    private readonly ObterTempoMedioExecucaoUseCase _tempoMedio;
    private readonly RegistrarDecisaoDeOrcamentoUseCase _registrarDecisao;
```

(b) Parâmetro do construtor — localizar:
```csharp
        ObterTempoMedioExecucaoUseCase tempoMedio)
    {
```
e substituir por:
```csharp
        ObterTempoMedioExecucaoUseCase tempoMedio,
        RegistrarDecisaoDeOrcamentoUseCase registrarDecisao)
    {
```

(c) Atribuição — localizar:
```csharp
        _tempoMedio = tempoMedio;
    }
```
e substituir por:
```csharp
        _tempoMedio = tempoMedio;
        _registrarDecisao = registrarDecisao;
    }
```

(d) Método — localizar (fim da classe, o método de métrica) e inserir logo após ele, antes da chave final:
```csharp
    public async Task<MetricasTempoMedioResponse> ObterTempoMedioExecucaoAsync(CancellationToken ct)
    {
        var metrica = await _tempoMedio.ExecutarAsync(ct);
        return OrdemDeServicoPresenter.ApresentarMetrica(metrica);
    }
```
e substituir por:
```csharp
    public async Task<MetricasTempoMedioResponse> ObterTempoMedioExecucaoAsync(CancellationToken ct)
    {
        var metrica = await _tempoMedio.ExecutarAsync(ct);
        return OrdemDeServicoPresenter.ApresentarMetrica(metrica);
    }

    public async Task<OrdemResponse?> RegistrarDecisaoDeOrcamentoAsync(Guid id, bool aprovado, CancellationToken ct)
    {
        var ordem = await _registrarDecisao.ExecutarAsync(id, aprovado, ct);
        return ordem is null ? null : OrdemDeServicoPresenter.Apresentar(ordem);
    }
```

- [ ] **Step 6: Criar o `WebhooksController` (Api)**

Create `src/Oficina.Api/Controllers/WebhooksController.cs`:
```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Oficina.Adaptadores.OrdensServico.Controllers;
using Oficina.Adaptadores.OrdensServico.Webhooks;
using Oficina.Aplicacao.OrdensServico.Dtos;
using Oficina.Api.Configuracao;

namespace Oficina.Api.Controllers;

// Webhook de aprovação externa de orçamento. Sem JWT ([AllowAnonymous]);
// autenticação por token de header (X-Webhook-Token) validado contra Webhook:Token.
[ApiController]
[AllowAnonymous]
[Route("api/v1/ordens-servico")]
public class WebhooksController : ControllerBase
{
    public const string NomeHeaderToken = "X-Webhook-Token";

    [HttpPost("{id:guid}/orcamento/aprovacao")]
    public async Task<IActionResult> RegistrarDecisao(
        Guid id,
        [FromBody] DecisaoOrcamentoRequest req,
        [FromHeader(Name = NomeHeaderToken)] string? token,
        [FromServices] OrdemDeServicoController controller,
        [FromServices] IOptions<WebhookOptions> webhook,
        CancellationToken ct)
    {
        if (!ValidadorTokenWebhook.EhTokenValido(token, webhook.Value.Token))
            return Unauthorized();

        if (!TentarInterpretarDecisao(req.Decisao, out var aprovado))
            return UnprocessableEntity(new ProblemDetails
            {
                Title = "Decisão inválida",
                Detail = "Use 'aprovado' ou 'recusado'.",
                Status = StatusCodes.Status422UnprocessableEntity
            });

        var resp = await controller.RegistrarDecisaoDeOrcamentoAsync(id, aprovado, ct);
        return resp is null ? NotFound() : Ok(resp);
    }

    private static bool TentarInterpretarDecisao(string? decisao, out bool aprovado)
    {
        aprovado = false;
        switch (decisao?.Trim().ToLowerInvariant())
        {
            case "aprovado": aprovado = true; return true;
            case "recusado": aprovado = false; return true;
            default: return false;
        }
    }
}
```
> Controllers da Api são descobertos automaticamente por `AddControllers()`/`MapControllers()` — não precisam de registro em DI. O `OrdemDeServicoController` (Adaptadores) e `IOptions<WebhookOptions>` são injetados via `[FromServices]`.

- [ ] **Step 7: Registrar o webhook no `Program.cs`**

Em `src/Oficina.Api/Program.cs`, localizar:
```csharp
builder.Services.AdicionarJwtBearer(builder.Configuration);
builder.Services.AdicionarPoliticas();
builder.Services.AdicionarRateLimit(builder.Configuration);
```
E substituir por:
```csharp
builder.Services.AdicionarJwtBearer(builder.Configuration);
builder.Services.AdicionarPoliticas();
builder.Services.AdicionarRateLimit(builder.Configuration);
builder.Services.AdicionarWebhook(builder.Configuration);
```

- [ ] **Step 8: Atualizar o DI de Aplicação (remove Aprovar/Rejeitar; add RegistrarDecisao)**

Em `src/Oficina.Aplicacao/DependencyInjectionAplicacao.cs`:

(a) Registrar o novo use case — localizar:
```csharp
        services.AddScoped<ObterTempoMedioExecucaoUseCase>();
```
e substituir por:
```csharp
        services.AddScoped<ObterTempoMedioExecucaoUseCase>();
        services.AddScoped<RegistrarDecisaoDeOrcamentoUseCase>();
```

(b) Remover os registros de consulta approve/reject — localizar:
```csharp
        // Consulta pública (sem auth)
        services.AddScoped<ConsultarOrdemPorNumeroUseCase>();
        services.AddScoped<AprovarOrcamentoPorClienteUseCase>();
        services.AddScoped<RejeitarOrcamentoPorClienteUseCase>();
```
e substituir por:
```csharp
        // Consulta pública (sem auth) — apenas leitura de status (anti-enumeração)
        services.AddScoped<ConsultarOrdemPorNumeroUseCase>();
```

- [ ] **Step 9: Remover os endpoints públicos de aprovar/rejeitar do `ConsultaController`**

Substituir `src/Oficina.Api/Controllers/ConsultaController.cs` inteiro por (mantém `GET` + `MapearResultado` anti-enumeração; remove as duas ações e o `using` do namespace `Consulta` que continua necessário só para `ConsultarOrdemPorNumeroUseCase` e os DTOs):
```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oficina.Aplicacao.Consulta;
using Oficina.Aplicacao.Consulta.Dtos;

namespace Oficina.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/v1/consulta")]
public class ConsultaController : ControllerBase
{
    [HttpGet("{numeroOs:long}")]
    public async Task<IActionResult> Consultar(
        long numeroOs,
        [FromQuery] string documento,
        [FromServices] ConsultarOrdemPorNumeroUseCase uc,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(documento))
            return BadRequest(new { erro = "Parâmetro 'documento' é obrigatório." });

        var r = await uc.ExecutarAsync(numeroOs, documento, ct);
        return MapearResultado(r);
    }

    private IActionResult MapearResultado(ResultadoConsulta r) => r switch
    {
        ResultadoConsulta.Sucesso s => Ok(s.Response),
        // Para evitar enumeração, ambos os casos retornam 404 com a mesma resposta
        ResultadoConsulta.NaoEncontrada => NotFound(),
        ResultadoConsulta.DocumentoNaoConfere => NotFound(),
        _ => StatusCode(StatusCodes.Status500InternalServerError)
    };
}
```

- [ ] **Step 10: Deletar os use cases e o teste de aprovação por cliente**

Run:
```bash
cd "C:/Users/thiago/source/repos/Fiap/fiap_15SOAT_fase1"
rm src/Oficina.Aplicacao/Consulta/AprovarOrcamentoPorClienteUseCase.cs
rm src/Oficina.Aplicacao/Consulta/RejeitarOrcamentoPorClienteUseCase.cs
rm tests/Oficina.Aplicacao.Testes/Consulta/AprovarOrcamentoPorClienteUseCaseTestes.cs
```
> `ConsultarOrdemPorNumeroUseCaseTestes.cs` **permanece** (testa o GET). Nenhum outro arquivo referencia os use cases deletados além do `DependencyInjectionAplicacao` (Step 8) e do `ConsultaController` (Step 9), já ajustados.

- [ ] **Step 11: Adicionar config do token — `.env.example`, `appsettings.json`, `docker-compose`**

(a) Em `.env.example`, localizar:
```
# ==========================================
# Ambiente
# ==========================================
ASPNETCORE_ENVIRONMENT=Development
```
e substituir por:
```
# ==========================================
# Webhook de aprovação de orçamento (Plano 06)
# ==========================================
WEBHOOK_TOKEN=troque-por-um-token-secreto-forte

# ==========================================
# Ambiente
# ==========================================
ASPNETCORE_ENVIRONMENT=Development
```

(b) Em `src/Oficina.Api/appsettings.json`, localizar:
```json
  "ConnectionStrings": {
    "Default": "Host=localhost;Port=5432;Database=oficina;Username=oficina;Password=oficina"
  },
  "AllowedHosts": "*"
```
e substituir por:
```json
  "ConnectionStrings": {
    "Default": "Host=localhost;Port=5432;Database=oficina;Username=oficina;Password=oficina"
  },
  "Webhook": {
    "Token": ""
  },
  "AllowedHosts": "*"
```
> Token vazio no arquivo base = **fail-closed** (`ValidadorTokenWebhook` recusa tudo até que `WEBHOOK_TOKEN`/`Webhook__Token` seja provido pelo ambiente).

(c) Em `docker/docker-compose.yml` (o compose completo muda em plano futuro — aqui só a env do serviço `api`), localizar:
```yaml
      AdminBootstrap__Password: ${ADMIN_BOOTSTRAP_PASSWORD}
```
e substituir por:
```yaml
      AdminBootstrap__Password: ${ADMIN_BOOTSTRAP_PASSWORD}
      Webhook__Token: ${WEBHOOK_TOKEN}
```

- [ ] **Step 12: Criar `RegistrarDecisaoDeOrcamentoUseCaseTestes`**

Create `tests/Oficina.Aplicacao.Testes/OrdensServico/RegistrarDecisaoDeOrcamentoUseCaseTestes.cs`:
```csharp
using FluentAssertions;
using Moq;
using Oficina.Aplicacao.OrdensServico;
using Oficina.Aplicacao.OrdensServico.Gateways;
using Oficina.Dominio.OrdensServico;
using Xunit;

namespace Oficina.Aplicacao.Testes.OrdensServico;

public class RegistrarDecisaoDeOrcamentoUseCaseTestes
{
    private readonly Mock<IOrdemDeServicoGateway> _ordens = new();
    private readonly Mock<INotificacaoGateway> _notificacoes = new();

    private RegistrarDecisaoDeOrcamentoUseCase CriarUseCase() =>
        new(_ordens.Object, _notificacoes.Object);

    private static OrdemDeServico OsAguardandoAprovacao()
    {
        var os = OrdemDeServico.Criar(Guid.NewGuid(), Guid.NewGuid());
        os.AdicionarItemServico(Guid.NewGuid(), "S", 10m, 1);
        os.IniciarDiagnostico();
        os.EnviarOrcamentoParaAprovacao();
        return os;
    }

    [Fact]
    public async Task Executar_Aprovado_DeveAprovarPersistirENotificar()
    {
        var os = OsAguardandoAprovacao();
        _ordens.Setup(o => o.ObterPorIdAsync(os.Id, It.IsAny<CancellationToken>())).ReturnsAsync(os);

        var resp = await CriarUseCase().ExecutarAsync(os.Id, true, default);

        resp.Should().NotBeNull();
        os.OrcamentoAprovadoEm.Should().NotBeNull();
        os.Status.Should().Be(StatusOrdemDeServico.AguardandoAprovacao); // Aprovar só carimba a data
        _ordens.Verify(o => o.SalvarAsync(It.IsAny<CancellationToken>()), Times.Once);
        _notificacoes.Verify(n => n.NotificarMudancaDeStatusAsync(os, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Executar_Recusado_DeveRejeitarECancelar()
    {
        var os = OsAguardandoAprovacao();
        _ordens.Setup(o => o.ObterPorIdAsync(os.Id, It.IsAny<CancellationToken>())).ReturnsAsync(os);

        var resp = await CriarUseCase().ExecutarAsync(os.Id, false, default);

        resp.Should().NotBeNull();
        os.Status.Should().Be(StatusOrdemDeServico.Cancelada);
        os.OrcamentoRejeitadoEm.Should().NotBeNull();
        _notificacoes.Verify(n => n.NotificarMudancaDeStatusAsync(os, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Executar_OsInexistente_DeveRetornarNull()
    {
        _ordens.Setup(o => o.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((OrdemDeServico?)null);

        var resp = await CriarUseCase().ExecutarAsync(Guid.NewGuid(), true, default);

        resp.Should().BeNull();
        _ordens.Verify(o => o.SalvarAsync(It.IsAny<CancellationToken>()), Times.Never);
        _notificacoes.Verify(n => n.NotificarMudancaDeStatusAsync(It.IsAny<OrdemDeServico>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Executar_EstadoInvalido_DevePropagarTransicaoInvalida()
    {
        // OS em Recebida — não pode aprovar
        var os = OrdemDeServico.Criar(Guid.NewGuid(), Guid.NewGuid());
        _ordens.Setup(o => o.ObterPorIdAsync(os.Id, It.IsAny<CancellationToken>())).ReturnsAsync(os);

        var act = async () => await CriarUseCase().ExecutarAsync(os.Id, true, default);

        await act.Should().ThrowAsync<TransicaoDeStatusInvalidaException>();
        _notificacoes.Verify(n => n.NotificarMudancaDeStatusAsync(It.IsAny<OrdemDeServico>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
```

- [ ] **Step 13: Criar `ValidadorTokenWebhookTestes`**

Create `tests/Oficina.Adaptadores.Testes/OrdensServico/ValidadorTokenWebhookTestes.cs`:
```csharp
using FluentAssertions;
using Oficina.Adaptadores.OrdensServico.Webhooks;

namespace Oficina.Adaptadores.Testes.OrdensServico;

public class ValidadorTokenWebhookTestes
{
    [Fact]
    public void TokenCorreto_DeveSerValido()
    {
        ValidadorTokenWebhook.EhTokenValido("segredo-123", "segredo-123").Should().BeTrue();
    }

    [Fact]
    public void TokenErrado_DeveSerInvalido()
    {
        ValidadorTokenWebhook.EhTokenValido("errado", "segredo-123").Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void TokenRecebidoAusente_DeveSerInvalido(string? recebido)
    {
        ValidadorTokenWebhook.EhTokenValido(recebido, "segredo-123").Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void TokenEsperadoNaoConfigurado_DeveSerInvalido_FailClosed(string? esperado)
    {
        ValidadorTokenWebhook.EhTokenValido("qualquer", esperado).Should().BeFalse();
    }

    [Fact]
    public void TamanhosDiferentes_DeveSerInvalido()
    {
        ValidadorTokenWebhook.EhTokenValido("abc", "abcdef").Should().BeFalse();
    }
}
```

- [ ] **Step 14: Estender o `CriarController` e adicionar teste de decisão**

Em `tests/Oficina.Adaptadores.Testes/OrdensServico/OrdemDeServicoControllerTestes.cs`:

(a) O construtor de `OrdemDeServicoController` ganhou `RegistrarDecisaoDeOrcamentoUseCase` (última posição). Atualizar o helper — localizar:
```csharp
            new ObterTempoMedioExecucaoUseCase(ordens.Object));
    }
```
e substituir por:
```csharp
            new ObterTempoMedioExecucaoUseCase(ordens.Object),
            new RegistrarDecisaoDeOrcamentoUseCase(ordens.Object, notificacoes.Object));
    }
```

(b) Adicionar um teste de decisão ao final da classe (antes da chave de fechamento):
```csharp
    [Fact]
    public async Task RegistrarDecisaoDeOrcamentoAsync_Aprovado_DeveRetornarOrdemResponse()
    {
        var ordens = new Mock<IOrdemDeServicoGateway>();
        var os = OrdemDeServico.Criar(Guid.NewGuid(), Guid.NewGuid());
        os.AdicionarItemServico(Guid.NewGuid(), "S", 10m, 1);
        os.IniciarDiagnostico();
        os.EnviarOrcamentoParaAprovacao();
        ordens.Setup(o => o.ObterPorIdAsync(os.Id, It.IsAny<CancellationToken>())).ReturnsAsync(os);

        var controller = CriarController(ordens, new Mock<IClienteGateway>(),
            new Mock<IServicoGateway>(), new Mock<IPecaGateway>());

        var resp = await controller.RegistrarDecisaoDeOrcamentoAsync(os.Id, true, default);

        resp.Should().NotBeNull();
        resp!.OrcamentoAprovadoEm.Should().NotBeNull();
        ordens.Verify(o => o.SalvarAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RegistrarDecisaoDeOrcamentoAsync_OsInexistente_DeveRetornarNull()
    {
        var ordens = new Mock<IOrdemDeServicoGateway>();
        ordens.Setup(o => o.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((OrdemDeServico?)null);
        var controller = CriarController(ordens, new Mock<IClienteGateway>(),
            new Mock<IServicoGateway>(), new Mock<IPecaGateway>());

        var resp = await controller.RegistrarDecisaoDeOrcamentoAsync(Guid.NewGuid(), true, default);

        resp.Should().BeNull();
    }
```

- [ ] **Step 15: Ajustar os testes de integração (CI) — Consulta e AuthFixture**

> **Docker indisponível localmente** — compilam no build local; rodam no CI.

(a) Em `tests/Oficina.Integracao.Testes/Auth/AuthFixture.cs`, localizar:
```csharp
        Environment.SetEnvironmentVariable("AdminBootstrap__Password", "AlteraMe@123");
```
e substituir por:
```csharp
        Environment.SetEnvironmentVariable("AdminBootstrap__Password", "AlteraMe@123");
        Environment.SetEnvironmentVariable("Webhook__Token", "token-teste-webhook");
```

(b) Em `tests/Oficina.Integracao.Testes/Consulta/ConsultaPublicaTestes.cs`, **remover** os dois testes que exercem as rotas públicas removidas (`Aprovar_PeloCliente_DeveTransitar` e `Rejeitar_PeloCliente_DeveCancelar`) e **adicionar** um teste do webhook. Localizar e remover:
```csharp
    [Fact]
    public async Task Aprovar_PeloCliente_DeveTransitar()
    {
        var (numero, doc) = await CriarOsParaConsultaAsync();
        var publico = _fx.Factory.CreateClient();

        var resp = await publico.PostAsync($"/api/v1/consulta/{numero}/aprovar?documento={doc}", null);

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadFromJsonAsync<ConsultaPublicaResponse>();
        body!.OrcamentoAprovadoEm.Should().NotBeNull();
    }

    [Fact]
    public async Task Rejeitar_PeloCliente_DeveCancelar()
    {
        var (numero, doc) = await CriarOsParaConsultaAsync();
        var publico = _fx.Factory.CreateClient();

        var resp = await publico.PostAsync($"/api/v1/consulta/{numero}/rejeitar?documento={doc}", null);

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadFromJsonAsync<ConsultaPublicaResponse>();
        body!.Status.Should().Be("Cancelada");
    }
```
E substituir esse trecho por um teste que precisa do **id** da OS. Como o helper `CriarOsParaConsultaAsync` retorna `(numero, documento)`, adicionar uma variação que devolve o `OrdemResponse` completo e um teste de webhook:
```csharp
    private async Task<OrdemResponse> CriarOsEnviadaAsync()
    {
        var http = await AdminAsync();
        var doc = "11144477735";

        var cli = await (await http.GetAsync($"/api/v1/clientes?documento={doc}"))
            .Content.ReadFromJsonAsync<ClienteResponse>();
        if (cli is null)
        {
            var r = await http.PostAsJsonAsync("/api/v1/clientes",
                new CriarClienteRequest("Cli OS", doc, $"c{Guid.NewGuid():N}@x.com", "11987654321"));
            cli = await r.Content.ReadFromJsonAsync<ClienteResponse>();
        }
        var v = await (await http.PostAsJsonAsync($"/api/v1/clientes/{cli!.Id}/veiculos",
            new AdicionarVeiculoRequest($"WHK{new Random().Next(1000,9999)}", "F", "U", 2020)))
            .Content.ReadFromJsonAsync<VeiculoResponse>();
        var s = await (await http.PostAsJsonAsync("/api/v1/servicos",
            new CriarServicoRequest("S webhook", "x", 10m, 10)))
            .Content.ReadFromJsonAsync<ServicoResponse>();

        var os = await (await http.PostAsJsonAsync("/api/v1/ordens-servico",
            new AbrirOrdemRequest(
                new ClienteDadosDto(doc, "Cli OS", $"c{Guid.NewGuid():N}@x.com", "11987654321"),
                new VeiculoDadosDto(v!.Placa, "F", "U", 2020),
                new[] { new ItemServicoDto(s!.Id, 1) },
                Array.Empty<ItemPecaDto>()))).Content.ReadFromJsonAsync<OrdemResponse>();
        await http.PatchAsync($"/api/v1/ordens-servico/{os!.Id}/diagnostico", null);
        await http.PostAsync($"/api/v1/ordens-servico/{os.Id}/orcamento/enviar", null);
        return os;
    }

    [Fact]
    public async Task Webhook_ComTokenValido_Aprovado_DeveRetornar200()
    {
        var os = await CriarOsEnviadaAsync();
        var publico = _fx.Factory.CreateClient();
        publico.DefaultRequestHeaders.Add("X-Webhook-Token", "token-teste-webhook");

        var resp = await publico.PostAsJsonAsync(
            $"/api/v1/ordens-servico/{os.Id}/orcamento/aprovacao",
            new DecisaoOrcamentoRequest("aprovado"));

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadFromJsonAsync<OrdemResponse>();
        body!.OrcamentoAprovadoEm.Should().NotBeNull();
    }

    [Fact]
    public async Task Webhook_SemToken_DeveRetornar401()
    {
        var os = await CriarOsEnviadaAsync();
        var publico = _fx.Factory.CreateClient();

        var resp = await publico.PostAsJsonAsync(
            $"/api/v1/ordens-servico/{os.Id}/orcamento/aprovacao",
            new DecisaoOrcamentoRequest("aprovado"));

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Webhook_DecisaoInvalida_DeveRetornar422()
    {
        var os = await CriarOsEnviadaAsync();
        var publico = _fx.Factory.CreateClient();
        publico.DefaultRequestHeaders.Add("X-Webhook-Token", "token-teste-webhook");

        var resp = await publico.PostAsJsonAsync(
            $"/api/v1/ordens-servico/{os.Id}/orcamento/aprovacao",
            new DecisaoOrcamentoRequest("talvez"));

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }
```
> Adicionar ao topo do arquivo os `using` que faltarem: `Oficina.Aplicacao.OrdensServico.Dtos` já está importado (usa `OrdemResponse`); `AbrirOrdemRequest`/`DecisaoOrcamentoRequest`/`ClienteDadosDto`/`VeiculoDadosDto`/`ItemServicoDto`/`ItemPecaDto` estão no mesmo namespace. Se o compilador reclamar de `ConsultaPublicaResponse` sem uso após a remoção, manter o `using Oficina.Aplicacao.Consulta.Dtos;` apenas se algum teste remanescente (os GET) ainda o usa — os testes `Consultar_*` continuam desserializando `ConsultaPublicaResponse`, então o `using` permanece.

- [ ] **Step 16: Verificar que não sobrou referência aos use cases/rotas removidos**

Run:
```bash
cd "C:/Users/thiago/source/repos/Fiap/fiap_15SOAT_fase1"
grep -rn "AprovarOrcamentoPorClienteUseCase\|RejeitarOrcamentoPorClienteUseCase\|/aprovar\|/rejeitar" src tests --include=*.cs
```
Expected: **nenhuma linha**. Se algo aparecer, remover antes de prosseguir. (As rotas `consulta/{n}/aprovar` e `/rejeitar` não existem mais; a aprovação é via webhook.)

- [ ] **Step 17: Compilar a solution**

Run:
```bash
cd "C:/Users/thiago/source/repos/Fiap/fiap_15SOAT_fase1"
dotnet build Oficina.sln
```
Expected: **Build succeeded**, 0 erros.

- [ ] **Step 18: Rodar os 3 projetos de teste unitário (um por chamada)**

Run:
```bash
cd "C:/Users/thiago/source/repos/Fiap/fiap_15SOAT_fase1"
dotnet test tests/Oficina.Dominio.Testes
dotnet test tests/Oficina.Aplicacao.Testes
dotnet test tests/Oficina.Adaptadores.Testes
```
Expected: **PASS** nos três. `RegistrarDecisaoDeOrcamentoUseCaseTestes` e `ValidadorTokenWebhookTestes` cobrem o novo fluxo e a segurança do token; os testes de webhook fim-a-fim rodam no CI (Docker).

- [ ] **Step 19: Commit**

```bash
cd "C:/Users/thiago/source/repos/Fiap/fiap_15SOAT_fase1"
git add -A
git commit -m "$(cat <<'EOF'
feat: webhook de aprovacao externa de orcamento (substitui rotas publicas)

Adiciona POST /api/v1/ordens-servico/{id}/orcamento/aprovacao
([AllowAnonymous]) protegido por token no header X-Webhook-Token comparado
a Webhook:Token (env WEBHOOK_TOKEN) com comparacao fixed-time e fail-closed.
Novo RegistrarDecisaoDeOrcamentoUseCase aplica Aprovar/Rejeitar, persiste e
notifica. Remove as rotas publicas consulta/{n}/aprovar e /rejeitar e seus
use cases; mantem GET /consulta/{n} e a anti-enumeracao. Adiciona
WEBHOOK_TOKEN ao .env.example, appsettings e compose.

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Auto-revisão (cobertura, consistência, riscos)

**Cobertura das 4 mudanças:** ✔ Task 1 (notificação mock + 5 transições), ✔ Task 2 (exclusão de terminais + ordenação por prioridade e `CriadaEm`), ✔ Task 3 (abertura consolidada atômica), ✔ Task 4 (webhook com token, remoção das rotas públicas). Cada Task termina com build + 3 projetos de teste unitário + 1 commit.

**Sem placeholders:** todo código é completo e compilável; caminhos e comandos são exatos (Git Bash). Não há `TODO`/`...`.

**Consistência de tipos/nomes/rotas:**
- `INotificacaoGateway.NotificarMudancaDeStatusAsync(OrdemDeServico, CancellationToken)` — mesma assinatura em porta, impl e todos os call sites; impl loga `"[e-mail] OS #{Numero} agora esta {Status} (cliente {ClienteId})"`.
- Construtores dos 5 use cases de transição, do `AbrirOrdemDeServicoUseCase`, do `RegistrarDecisaoDeOrcamentoUseCase` e do `OrdemDeServicoController` batem com as chamadas em DI e no helper `CriarController` (atualizado nas Tasks 1/3/4, na ordem correta dos parâmetros: `Abrir` entra em 2ª posição no ctor do controller; `RegistrarDecisao` entra em última).
- Rotas: `POST /api/v1/ordens-servico` (contrato novo `AbrirOrdemRequest`, 201 `CreatedAtAction(nameof(Obter))`); webhook `POST /api/v1/ordens-servico/{id:guid}/orcamento/aprovacao`; `GET /api/v1/consulta/{numeroOs}` mantido; `consulta/.../aprovar|rejeitar` removidos (verificado por `grep` no Step 16 da Task 4).
- `StatusOrdemDeServico` (enum `Recebida=1..Cancelada=7`) usado na prioridade e no filtro; prioridade EmExecucao<AguardandoAprovacao<EmDiagnostico<Recebida conforme enunciado.

**Atomicidade da abertura:** um único `_ordens.SalvarAsync(ct)` no fim; os 4 gateways resolvem DataSources sobre o **mesmo** `OficinaDbContext` scoped, então um `SaveChanges` commita cliente+veículo+OS+itens juntos. Cliente novo → `AdicionarAsync` insere o grafo (cliente+veículo); cliente existente com veículo novo → `MarcarVeiculoComoNovo` (workaround de change tracking). Nenhum `SalvarAsync` intermediário. Falha de validação (serviço/peça inválido) lança antes do `SalvarAsync` → nada é persistido.

**Segurança do token:** comparação em tempo constante (`CryptographicOperations.FixedTimeEquals`) com verificação de tamanho; **fail-closed** quando o token configurado é vazio/nulo (recusa tudo); token base vazio no `appsettings.json`; token real só via ambiente. 401 (token) tem precedência sobre 422 (decisão inválida). Lógica de comparação isolada em helper puro unit-testado (feliz/errado/ausente/fail-closed/tamanho).

**Riscos / pontos a revisar no code review:**
- **Integração roda só no CI** (sem Docker local). As edições em `OrdensServicoFluxoTestes`, `ConsultaPublicaTestes` e `AuthFixture` são compiladas no build local mas **não executadas** aqui — revisar no CI. Um detalhe frágil: assumi que `VeiculoResponse` expõe uma propriedade `Placa` (string) para reaproveitar a placa na abertura consolidada; se o nome diferir, usar a placa literal gerada no POST do veículo (indicado inline nos Steps).
- **FluentValidation vs. domínio:** `AbrirOrdemValidator` cobre presença/limites (400 via auto-validação); formatos de documento/placa/e-mail continuam validados no domínio (422 via middleware). Comportamento coerente com o resto da app, mas a mistura 400/422 deve ser conhecida por quem consome a API.
- **Notificação como efeito colateral:** se a impl real (futura) lançar, isso poderia propagar como 500 após a OS já ter sido persistida (para transições fora de transação). No mock atual não lança; ao trocar por impl real, considerar try/catch + retry/outbox. Documentado, fora do escopo deste plano.
- **`CriarOrdemUseCase` mantido sem endpoint:** decisão consciente para minimizar churn; se o time preferir removê-lo, seria uma limpeza separada (removeria `CriarOrdemRequest`, `CriarOrdemValidator`, `CriarAsync`, os testes de `CriarOrdem*` e a linha de DI).
