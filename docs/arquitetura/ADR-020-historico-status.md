# ADR-020 — Histórico de status como tabela dedicada

**Status:** Aceita
**Data:** 2026-09-15

## Contexto

Antes da Fase 3, `ordem_servico` guardava só o status atual (`status`, mais os timestamps
de cada marco: `diagnosticada_em`, `iniciada_em`, `finalizada_em` etc.). Sem um registro de
**quando** cada transição de status ocorreu — e de quanto tempo durou o estado anterior —
o painel "tempo médio de execução por status", exigido pela Fase 3, é impossível de
calcular a partir do modelo: os timestamps do agregado marcam o momento de cada marco, mas
não formam uma sequência uniforme de (status anterior, status novo, quando, duração do
anterior) para qualquer transição.

## Decisão

Tabela dedicada `os.historico_status`, criada pela migration
`20260914125322_HistoricoStatusEUnidade` e mapeada em `HistoricoStatusConfiguration.cs`:
`id` (`uuid`, PK), `status_anterior` (`character varying(30)`, nulável — nulo só na
primeira entrada, quando não existe status anterior), `status_novo`
(`character varying(30)`, obrigatório), `ocorrido_em` (`timestamp with time zone`,
obrigatório), `duracao_segundos` (`bigint`, nulável), `usuario_id` (`uuid`, nulável) e
`ordem_servico_id` (`uuid`, FK para `os.ordem_servico.id`, `ON DELETE CASCADE`). Dois
índices compostos sustentam as duas consultas reais: `ix_historico_os`
(`ordem_servico_id`, `ocorrido_em`) para a linha do tempo de uma OS, e
`ix_historico_status_data` (`status_anterior`, `ocorrido_em`) para o tempo médio por
status de origem numa janela — ambos comentados no próprio arquivo de configuração do EF
Core.

A escrita acontece na **mesma transação** da mudança de estado. `OrdemDeServico.Criar` e
cada método público de transição (`IniciarDiagnostico`, `EnviarParaAprovacao` etc.) chamam
o método privado `RegistrarTransicao`, que calcula `duracao_segundos` a partir da última
entrada de `Historico` e adiciona a nova entrada à coleção do próprio agregado — sem
`Add()` explícito no `DbContext`. Como essa coleção é uma navigation property de uma
entidade já rastreada, o change tracker do EF Core marca a nova entrada como `Added`
sozinho, e um único `SaveChangesAsync` (chamado pelos casos de uso via
`IOrdemDeServicoGateway.SalvarAsync`) grava OS, itens e histórico numa única transação
implícita. A mesma tabela serve tanto o dashboard (consulta direta ao Postgres) quanto o
custom event `OrdemServicoEvento`, publicado por
`PublicadorEventoOsExtensions.ExecutarTransicaoComTelemetriaAsync` a partir da última
entrada do histórico, depois que a persistência tem sucesso.

Como parágrafo desta decisão, a alternativa descartada: derivar a duração dos eventos já
enviados ao New Relic, em vez de manter uma tabela própria. Recusada porque prenderia o
cálculo à retenção da ferramenta de observabilidade — o histórico desapareceria se a
licença expirasse ou a retenção do evento custom vencesse —, enquanto o Postgres já é a
fonte de verdade transacional do resto do domínio.

Uma revisão de um plano anterior (Plano 1) identificou dois defeitos que ficavam
silenciosos com a suíte de testes verde e só apareceriam no painel vazio: (1)
`duracao_segundos` nasceria **sempre nulo**, mesmo em transições seguintes à primeira,
porque a leitura da OS não carregava a coleção `Historico` da qual `RegistrarTransicao`
lê (`_historico.LastOrDefault()`) para calcular a duração; (2) o evento
`OrdemServicoEvento` com `statusNovo = "Recebida"` **nunca seria emitido**, porque a
criação da OS não estava instrumentada com a mesma telemetria das demais transições.
Confirmado no código atual que os dois estão corrigidos hoje: `ObterPorIdAsync` e
`ObterPorNumeroAsync` em `OrdemDeServicoDataSource`
(`src/Oficina.Infraestrutura/Persistencia/DataSources/OrdemDeServicoDataSource.cs`) fazem
`.Include(o => o.Historico.OrderBy(h => h.OcorridoEm))` antes de devolver a OS; e tanto
`AbrirOrdemDeServicoUseCase` (o caminho real de `POST /api/v1/ordens-servico`) quanto
`CriarOrdemUseCase` envolvem a persistência em
`_publicador.ExecutarTransicaoComTelemetriaAsync`, que publica o evento de sucesso a
partir da última transição do histórico assim que `SaveChangesAsync` retorna sem erro.
Isso é o que justifica escrever o histórico na mesma transação da OS: sem o histórico
carregado na leitura e sem a criação instrumentada no mesmo caminho da persistência, os
dois defeitos acima voltam a existir — e nenhum teste os pega, porque ambos falham por
omissão (um campo nulo, um evento que não sai), não por exceção.

**V15**: `historico_status.usuario_id` é sempre `NULL`, porque nenhum caso de uso propaga
a identidade do chamador até o agregado — `RegistrarTransicao` chama
`HistoricoStatus.Criar` sem o parâmetro opcional `usuarioId`. O mesmo vale para
`ordem_servico.unidade`: `OrdemDeServico.Criar(Guid clienteId, Guid veiculoId, string?
observacoes = null, string unidade = "matriz")` tem `unidade` com valor padrão, e nenhum
chamador — nem `AbrirOrdemDeServicoUseCase`, nem `CriarOrdemUseCase` — o informa, logo toda
OS nasce `"matriz"`. Alimentar os dois exigiria origem da unidade (token, request ou
configuração do pod) e propagação do usuário autenticado até o domínio; está fora do
escopo desta fase.

## Consequências

- ✅ O dashboard de tempo médio por status tem fonte de verdade no próprio banco,
  independente da retenção do New Relic
- ✅ Evento de negócio e tabela de histórico não divergem: os dois nascem da mesma última
  entrada de `Historico`, no mesmo caminho de código
- ⚠️ Uma escrita a mais por transição de status (`INSERT` em `os.historico_status` a cada
  mudança)
- ⚠️ A tabela cresce sem política de retenção — nenhuma rotina apaga entradas antigas
- ⚠️ `usuario_id` e `unidade` são pontos de extensão declarados, não funcionalidade: um
  leitor apressado do schema pode concluir que a auditoria por usuário e a segmentação por
  unidade já funcionam

Diagrama relacionado: [modelo entidade-relacionamento](diagramas/entidade-relacionamento.md);
sequência: [abertura de ordem de serviço](diagramas/sequencia-abertura-os.md).
