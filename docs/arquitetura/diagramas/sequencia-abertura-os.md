# Sequência — abertura de ordem de serviço

Caminho completo de `POST /api/v1/ordens-servico`, do token ao evento de negócio que alimenta o
dashboard. Tabela do histórico de status em [ADR-020](../ADR-020-historico-status.md);
observabilidade em [RFC-005](../../rfc/RFC-005-observabilidade-new-relic.md).

```mermaid
sequenceDiagram
    autonumber
    actor A as Atendente
    participant GW as API Gateway<br/>HTTP API
    participant AZ as Lambda Authorizer<br/>oficina-auth-authorizer
    participant NLB as VPC Link → NLB interno
    participant API as oficina-api<br/>pod em oficina-prd
    participant DB as RDS PostgreSQL
    participant NR as New Relic

    A->>GW: POST /api/v1/ordens-servico<br/>Authorization: Bearer <jwt><br/>X-Correlation-Id
    GW->>AZ: authorizer REQUEST (payload 2.0)
    Note over AZ: cache de 300 s por header Authorization<br/>(authorizer_result_ttl_in_seconds, default do Terraform)
    AZ->>AZ: valida HS256, iss=oficina-auth, aud=oficina-api, exp
    AZ-->>GW: { isAuthorized: true, context: { perfil, sub, documento } }
    GW->>NLB: HTTP_PROXY + headers X-Perfil, X-Sub, X-Documento
    NLB->>API: :80 → NodePort 30080
    Note over API: MiddlewareDeCorrelacao (1º da pipeline) fixa correlationId no LogContext:<br/>X-Correlation-Id do cliente, senão X-Amzn-RequestId, senão novo Guid
    Note over API: UseAuthentication revalida o JWT direto do Authorization Bearer<br/>(HS256, iss=oficina-auth, aud=oficina-api, exp) — X-Perfil/X-Sub/X-Documento<br/>não são lidos pela API, são só o que o API Gateway repassou
    API->>API: UseAuthorization aplica a policy RequerAdminOuAtendente<br/>(claim "perfil" = Admin ou Atendente, senão 403)
    API->>API: OrdensServicoController.Abrir → OrdemDeServicoController.AbrirAsync<br/>→ AbrirOrdemDeServicoUseCase.ExecutarAsync<br/>(find-or-create cliente por documento, veículo por placa)
    API->>DB: BEGIN (transação implícita do SaveChangesAsync)
    API->>DB: INSERT os.ordem_servico (status "Recebida", unidade "matriz")
    API->>DB: INSERT os.item_servico / os.item_peca (se enviados no request)
    API->>DB: INSERT os.historico_status (status_anterior NULL, status_novo "Recebida",<br/>duracao_segundos NULL, usuario_id NULL)
    API->>DB: COMMIT
    API->>NR: RecordCustomEvent OrdemServicoEvento<br/>{ numeroOs, statusAnterior: "(inicial)", statusNovo: "Recebida",<br/>resultado: "Sucesso", unidade: "matriz" }
    API->>NR: log JSON (Serilog) com trace.id/span.id (agente do New Relic) e correlationId (LogContext)
    API-->>A: 201 Created + header X-Correlation-Id
```

## Pontos que não são óbvios no desenho

| Ponto | Por quê |
|---|---|
| A API revalida o JWT | alcançar o NLB por dentro da VPC não pode bastar para usar rota protegida; os headers `X-Perfil`/`X-Sub`/`X-Documento` que o API Gateway repassa são informativos — a API nem os lê, ela decide tudo a partir das claims do próprio token (`ConfiguracaoJwt`, `PoliticasDeAutorizacao`) |
| `MiddlewareDeCorrelacao` roda antes da autenticação | é o primeiro middleware do pipeline (`Program.cs`); o `correlationId` entra no `LogContext` mesmo que a requisição venha a ser barrada por `UseAuthentication`/`UseAuthorization` logo depois |
| O caso de uso real é `AbrirOrdemDeServicoUseCase`, não `CriarOrdemUseCase` | `CriarOrdemUseCase` existe e está registrado no DI, mas nenhum endpoint de `OrdensServicoController` o chama; o `POST` de fato passa por `OrdemDeServicoController.AbrirAsync` → `AbrirOrdemDeServicoUseCase`, que também resolve cliente (por documento) e veículo (por placa) find-or-create antes de criar a OS |
| `historico_status` na mesma transação da OS | histórico é efeito colateral do domínio (`OrdemDeServico.RegistrarTransicao`), sem `Add()` explícito; o `ChangeTracker.Tracked` do `OficinaDbContext` marca a entrada como `Added` e um único `SaveChangesAsync` grava OS + itens + histórico numa transação implícita — se a escrita do histórico ficasse fora, o dashboard de tempo médio por status divergiria do estado real |
| `duracao_segundos` NULL nesta entrada específica é esperado | é a primeira entrada do histórico da OS (não há transição anterior para medir); o defeito real de uma revisão anterior — a leitura não incluía `Historico` e a duração saía sempre nula **mesmo em transições seguintes** — já está corrigido em `OrdemDeServicoDataSource.ObterPorIdAsync`/`ObterPorNumeroAsync`, que agora fazem `Include(o => o.Historico.OrderBy(h => h.OcorridoEm))` |
| `OrdemServicoEvento` com `statusNovo = "Recebida"` na criação | antes de uma correção anterior a criação da OS não estava instrumentada; hoje `AbrirOrdemDeServicoUseCase` publica o evento via `ExecutarTransicaoComTelemetriaAsync`, senão o painel de volume diário nasce vazio |
| `unidade` sempre `"matriz"` e `historico_status.usuario_id` sempre `NULL` | nenhum request, header ou configuração alimenta essas colunas (V15) — ver [ADR-020](../ADR-020-historico-status.md) |
| Rota de gestão exige perfil `Admin` ou `Atendente` | `[Authorize(Policy = PoliticasDeAutorizacao.RequerAdminOuAtendente)]` no nível do controller; um token de `Cliente` recebe 403 |
