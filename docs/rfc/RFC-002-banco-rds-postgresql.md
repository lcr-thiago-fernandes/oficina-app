# RFC-002 — Banco gerenciado: RDS PostgreSQL 16

**Status:** Aceita
**Data:** 2026-09-15
**Autor:** Thiago Fernandes
**ADRs relacionados:** ADR-002, ADR-010, ADR-020

## 1. Contexto e problema

O domínio da oficina é relacional por natureza — cliente, veículo, ordem de serviço,
itens, peças e movimentações de estoque se referenciam entre si, e várias operações
precisam ser atômicas mesmo cruzando tabelas: aprovar um orçamento e baixar peças do
estoque, ou abrir uma ordem de serviço (OS) e registrar sua primeira entrada de
histórico, têm que confirmar juntas ou falhar juntas. A Fase 2 já havia escolhido
PostgreSQL sobre esse critério ([ADR-002](../arquitetura/ADR-002-postgresql.md)) e
provisionado o banco como RDS ([ADR-010](../arquitetura/ADR-010-aws-eks-rds-terraform.md)).
A Fase 3 acrescenta um requisito novo sobre o mesmo banco: um painel de "tempo médio de
execução por status" da OS, que depende de uma sequência auditável de transições — algo
que o schema da Fase 2, com só o status atual e os timestamps de cada marco, não
sustenta. Este RFC decide duas coisas: se o banco gerenciado continua sendo a escolha
certa para a Fase 3 (e entre quais engines), e como o schema muda para sustentar o
painel sem perder a garantia transacional que já existia.

## 2. Alternativas consideradas

| Critério | RDS PostgreSQL | Aurora Serverless v2 | DynamoDB | PostgreSQL em contêiner no EKS |
|---|---|---|---|---|
| Custo mínimo mensal | Fixo e baixo: `db.t3.micro` sob demanda, ~US$ 15,00/mês pela tabela de custo da seção "Custo estimado" do [`fase3-design-arquitetural.md`](../arquitetura/fase3-design-arquitetural.md) | Cobra por ACU mesmo ocioso — há um piso de capacidade sempre ativo, cobrado enquanto o cluster existir, mesmo fora das janelas de demonstração | Sob demanda (por requisição/armazenamento), mas o modelo de custo não é o problema — é o de dados (linha abaixo) | Zero custo de serviço gerenciado, mas soma aos nós do EKS já provisionados (CPU/memória do pod) |
| Transação multi-tabela | Nativa: `SaveChangesAsync` do EF Core grava OS, itens e histórico numa única transação (ver [ADR-020](../arquitetura/ADR-020-historico-status.md)) | Mesma engine PostgreSQL por baixo, transação nativa igual ao RDS | Sem transação multi-item nativa entre tabelas (itens) equivalente a um `JOIN`; exigiria redesenhar o domínio em torno de chaves de partição | Nativa, mesma engine |
| Operação (backup, patching) | Backup automático e `auto_minor_version_upgrade` geridos pelo serviço | Igual ao RDS nesse quesito | Sem backup/restore relacional; snapshots do DynamoDB não substituem `pg_dump` de um schema relacional | Backup e patching viram trabalho manual (cron de `pg_dump`, atualização de imagem, PVC) |
| Adequação ao modelo | Alta: o domínio já é relacional com FKs físicas e schemas por bounded context ([ADR-002](../arquitetura/ADR-002-postgresql.md)) | Alta (mesma engine), mas sem ganho sobre o RDS neste volume | Baixa: o dashboard depende de agregações por faixa de tempo (`AVG` de duração por status numa janela) — natural em SQL, custoso em modelo de item único sem motor de agregação embutido | Alta, mas move a responsabilidade operacional para dentro do cluster |

**Por que DynamoDB não serve:** o modelo é relacional — OS referencia cliente, veículo,
itens de peça e itens de serviço, e o painel de tempo médio por status precisa agregar
`os.historico_status` por `status_anterior` numa janela de tempo (`AVG(duracao_segundos)
FACET status_anterior`, na formulação da consulta NRQL do painel — ver "Queries dos
painéis" em
[`fase3-design-arquitetural.md`](../arquitetura/fase3-design-arquitetural.md)).
Modelar isso em DynamoDB exigiria ou
duplicar dados em índices secundários para cada padrão de acesso, ou mover a agregação
para fora do banco — custo de modelagem que o domínio relacional já resolve de graça com
um índice composto.

**Por que Aurora Serverless v2 foi recusada:** o ambiente da Fase 3 fica desligado
(`terraform destroy`) na maior parte do tempo, fora das janelas de demonstração — é
assim que o custo do conjunto se mantém baixo (ver a tabela de custo do
[RFC-001](RFC-001-nuvem-aws.md)). O Aurora Serverless v2 cobra por uma capacidade
mínima de ACU que fica ativa continuamente enquanto o cluster existe, mesmo sem
tráfego — um piso que não se paga num ambiente que passa a maior parte do tempo fora
do ar, ao contrário de uma instância RDS convencional, que só existe (e só é cobrada)
enquanto o `apply` estiver de pé.

## 3. Decisão

Manter **RDS PostgreSQL 16** (`aws_db_instance.oficina`,
`oficina-infra-db/terraform/rds.tf`), com os mesmos parâmetros de infraestrutura da
Fase 2 e acréscimos específicos da Fase 3:

| Parâmetro | Valor | Fonte |
|---|---|---|
| `instance_class` | `db.t3.micro` | `oficina-infra-db/terraform/variables.tf` (`db_instance_class`) |
| `allocated_storage` / `storage_type` | 20 GB / `gp3` | `oficina-infra-db/terraform/rds.tf`, `variables.tf` (`db_allocated_storage`) |
| `multi_az` | `false` (single-AZ) | `oficina-infra-db/terraform/rds.tf` |
| `storage_encrypted` | `true` | `oficina-infra-db/terraform/rds.tf` |
| `publicly_accessible` | `false`, subnet privada | `oficina-infra-db/terraform/rds.tf` |
| `engine_version` | `"16"` (só a major) | `oficina-infra-db/terraform/variables.tf` (`db_engine_version`) |
| `auto_minor_version_upgrade` | `true` | `oficina-infra-db/terraform/rds.tf` |

Acréscimos da Fase 3 sobre a base da Fase 2, cada um com o motivo:

- **Parameter group customizado** (`aws_db_parameter_group.postgres`,
  `oficina-infra-db/terraform/rds.tf`) com `log_min_duration_statement` dinâmico
  (`apply_method = "immediate"`, sem reboot), valor lido de
  `var.db_log_min_duration_statement_ms` (default `500`,
  `oficina-infra-db/terraform/variables.tf`): loga statements acima de 500 ms para dar
  visibilidade a queries lentas sem instrumentação de aplicação.
- **Logs `postgresql` exportados para o CloudWatch** (`enabled_cloudwatch_logs_exports =
  ["postgresql"]`, `oficina-infra-db/terraform/rds.tf`), com o log group
  `aws_cloudwatch_log_group.postgresql` **pré-criado antes da instância**
  (`depends_on = [aws_cloudwatch_log_group.postgresql]`) e retenção de
  `var.log_retention_days` = **14 dias** (`oficina-infra-db/terraform/variables.tf`,
  decisão D5 do `oficina-infra-db/README.md`, item "Decisões e limitações
  registradas"). O log group precisa existir antes do RDS: se o próprio serviço o
  criasse ao exportar, ele nasceria sem expiração configurada, e o recurso Terraform
  falharia depois com `ResourceAlreadyExists`. É o que dá utilidade prática ao
  `log_min_duration_statement` — sem exportação, o log ficaria só na instância, sem
  retenção gerida.
- **`backup_retention_period`** = `var.db_backup_retention_days` (default `1` dia,
  `oficina-infra-db/terraform/variables.tf`).
- **`auto_minor_version_upgrade = true`** com `engine_version = "16"` (decisão D6,
  `oficina-infra-db/README.md`): a major fica fixa no Terraform, e o RDS escolhe e
  aplica a minor automaticamente — evita fixar uma minor específica que pode deixar
  de estar disponível na região.
- **Performance Insights** (`performance_insights_enabled = true`,
  `performance_insights_retention_period = var.db_performance_insights_retention_days`,
  default 7 dias — nível gratuito, `oficina-infra-db/terraform/rds.tf` e
  `variables.tf`): habilitado no Terraform para dar visibilidade a `db.t3.micro` sem
  overhead de configuração manual. **Isto está declarado no código, não observado em
  execução** — nada foi aplicado na AWS até a data deste documento, e o próprio
  `oficina-infra-db/README.md` registra como decisão D10 que o suporte de Performance
  Insights nessa classe de instância precisa ser conferido no primeiro `apply` real.

### V7 — senha gerada no `apply`, sem GitHub Secret

A senha do usuário master (`oficina_admin`) é gerada por `random_password.db`
(`oficina-infra-db/terraform/senha.tf`): **32 caracteres, apenas alfanuméricos**
(`special = false`, com `min_lower`, `min_upper` e `min_numeric` = 1 cada) — nunca
digitada, nunca passada por GitHub Secret. Não existe `DB_PASSWORD` em nenhum dos
quatro repositórios (confirmado em `oficina-infra-db/README.md`, seção "Deploy":
"**Não há** `DB_PASSWORD`: a senha nasce no `apply`"). O valor é gravado em texto puro
(nunca JSON) em `oficina/db_password` via `aws_secretsmanager_secret` +
`aws_secretsmanager_secret_version` (`oficina-infra-db/terraform/senha.tf`), com
`recovery_window_in_days = 0` — sem essa configuração, o nome do segredo ficaria
bloqueado por 7 a 30 dias após um `destroy`, e o próximo `apply` falharia com
"scheduled for deletion", incompatível com um ambiente que é destruído e recriado
entre janelas de demonstração.

A senha é alfanumérica porque o CD do `oficina-app` monta a connection string por
concatenação direta — `Password=${DB_PASSWORD}`, sem aspas (comentário em
`oficina-infra-db/terraform/senha.tf`: "`special = false`: o CD do oficina-app monta a
connection string por concatenacao (...), sem aspas, e o RDS proibe `/ @ " e espaco`");
qualquer caractere especial nesse formato quebraria o parsing da connection string em
pelo menos um dos dois consumidores (`oficina-app` e `oficina-auth-api`, ambos lendo o
mesmo segredo — `oficina-infra-db/docs/contratos.md`).

A alternativa nativa do RDS, `manage_master_user_password` (senha gerida
automaticamente pelo próprio serviço via Secrets Manager), foi recusada: esse
mecanismo grava o segredo em um nome próprio, escolhido pelo RDS, não em
`oficina/db_password` — quebraria o contrato de nome fixo que o CD do `oficina-app` e
a `oficina-auth-api` já esperam (`oficina-infra-db/docs/contratos.md`, seção "O que
este repositório CRIA no Secrets Manager"), além de não garantir a restrição a
caracteres alfanuméricos que a concatenação sem aspas exige.

### V13 — SG do RDS sem regras inline

`aws_security_group.rds` (`oficina-infra-db/terraform/rede.tf`) é criado **sem nenhum
bloco `ingress`/`egress` inline** — nem `ingress = []`. As regras existem como recursos
separados: `aws_vpc_security_group_ingress_rule.rds_recebe_dos_nos` (`5432 ←
sg-nodes`) e `aws_vpc_security_group_egress_rule.rds_saida` (egress liberado), ambos
neste mesmo arquivo. O motivo é o SG ser **compartilhado entre dois states**: o
`oficina-lambda-auth` acrescenta, a partir do próprio state dele,
`aws_vpc_security_group_ingress_rule.rds_recebe_da_lambda` (`5432 ← sg-lambda-auth`)
neste mesmo `security_group_id`. Se `aws_security_group.rds` declarasse blocos inline,
cada `apply` de um dos dois repositórios recalcularia a lista de regras a partir do que
o próprio state conhece e apagaria a regra criada pelo outro — o comentário em
`oficina-infra-db/terraform/rede.tf` é explícito: "qualquer bloco inline faria cada
apply daqui apagar a regra da Lambda (e vice-versa)". O job `contratos` do CI do
`oficina-infra-db` reprova qualquer regressão a bloco inline
(`oficina-infra-db/docs/contratos.md`).

### O defeito do Plano 4: `parameter_group` e troca de major

Uma revisão do Plano 4 identificou que `aws_db_parameter_group.postgres` com `name`
fixo e sem `create_before_destroy` travaria o `apply` no meio de uma futura troca de
major do PostgreSQL: `name` e `family` (`local.nome_param_group`,
`local.familia_parametros` em `oficina-infra-db/terraform/locals.tf`, derivados de
`var.db_engine_version`) mudam juntos quando a major muda, então o Terraform precisa
destruir o parameter group antigo e criar um novo — e, sem
`create_before_destroy`, o destroy aconteceria **antes** da instância migrar para o
novo grupo, e o RDS recusaria com `InvalidDBParameterGroupState` (o grupo antigo ainda
está em uso pela instância no momento em que o Terraform tentaria apagá-lo). O que
está no `.tf` atual resolve isso: `aws_db_parameter_group.postgres`
(`oficina-infra-db/terraform/rds.tf`) tem

```hcl
lifecycle {
  create_before_destroy = true
}
```

com o comentário no próprio arquivo confirmando o raciocínio ("name e family mudam
juntos ao trocar a major (...): cria o novo grupo, migra a instancia e so entao apaga
o antigo. Sem isto o destroy vem primeiro e o RDS recusa
(InvalidDBParameterGroupState)"). Com `create_before_destroy`, o Terraform cria o
parameter group novo, atualiza `aws_db_instance.oficina.parameter_group_name` para
apontar para ele, e só então destrói o antigo — sem a instância ficar, em nenhum
momento, referenciando um parameter group que deixou de existir.

## 4. Consequências

- ✅ Backup automático, patching de minor version e Performance Insights sem custo
  operacional de administrar um banco — o serviço gerido cobre os três.
- ✅ Transação abrangendo OS, itens e histórico de status continua nativa, no mesmo
  `SaveChangesAsync` ([ADR-020](../arquitetura/ADR-020-historico-status.md)) — nenhuma
  reformulação de domínio foi necessária para sustentar o painel novo da Fase 3.
- ⚠️ **Postura de demonstração, não de produção**: single-AZ, `backup_retention_period`
  de 1 dia e `skip_final_snapshot = true` sem `deletion_protection` significam que o
  ambiente é deliberadamente destruível — um `terraform destroy` apaga os dados sem
  snapshot de saída, e uma falha de AZ não tem failover automático.
- ⚠️ A senha só existe no state do Terraform (bucket S3 criptografado e versionado) e
  no Secrets Manager: perder os dois significa rotacionar a senha manualmente — não há
  cópia impressa ou em GitHub Secret em lugar nenhum.
- ⚠️ Performance Insights em `db.t3.micro` está **declarado no Terraform**, não
  observado em execução: nada foi aplicado na AWS até a data deste documento, e a
  decisão D10 do `oficina-infra-db/README.md` já prevê a necessidade de conferir o
  suporte real no primeiro `apply`.

## 5. Como isto está implementado

| O quê | Onde |
|---|---|
| Instância RDS, parameter group e log group | `oficina-infra-db/terraform/rds.tf` |
| Senha gerada no `apply` e segredo em texto puro (V7) | `oficina-infra-db/terraform/senha.tf` |
| SG do RDS, sem regras inline, e regra de ingress dos nós (V13) | `oficina-infra-db/terraform/rede.tf` |
| Publicação do endpoint e do SG no SSM | `oficina-infra-db/terraform/ssm.tf` |
| Valores de instância, storage, retenção e versão da engine | `oficina-infra-db/terraform/variables.tf` |
| Decisões numeradas D1–D14 do banco (referência cruzada) | `oficina-infra-db/README.md`, seção "Decisões e limitações registradas" |
| Contrato deste repositório com os demais (senha, endpoint, SG) | `oficina-infra-db/docs/contratos.md` |
| Decisão original de PostgreSQL como engine (Fase 2) | `oficina-app/docs/arquitetura/ADR-002-postgresql.md` |
| Decisão original de provisionar em RDS (Fase 2) | `oficina-app/docs/arquitetura/ADR-010-aws-eks-rds-terraform.md` |
| Tabela de custo estimado (origem do valor da seção 2) | `oficina-app/docs/arquitetura/fase3-design-arquitetural.md`, seção "Custo estimado" |
| Diagnóstico do schema e ajustes de modelagem (fonte da seção 6) | `oficina-app/docs/arquitetura/fase3-design-arquitetural.md`, seção "6. Modelagem do banco" |

## 6. Modelo de dados

### Diagnóstico do schema da Fase 2

O `fase3-design-arquitetural.md` (seção "6. Modelagem do banco", "Diagnóstico")
registra que o schema da Fase 2 já chegava indexado. Cada item foi conferido contra o
snapshot real do EF Core
(`src/Oficina.Infraestrutura/Migrations/OficinaDbContextModelSnapshot.cs`) e contra as
configurações (`src/Oficina.Infraestrutura/Persistencia/Configuracoes/`):

| Item do diagnóstico | Existe hoje? | Onde no snapshot/configuração |
|---|---|---|
| `cliente.documento` unique | Sim | `OficinaDbContextModelSnapshot.cs:471` (`OwnsOne("...Documento"...)`) e `:490-491` (`HasIndex("Valor").IsUnique()`, mapeado como `IX_cliente_documento`) |
| `os.numero` unique | Sim | `OficinaDbContextModelSnapshot.cs:457-458` (`HasIndex("Numero").IsUnique()`) |
| Índice em `os.status` | **Substituído** — não existe mais como índice de coluna única | O índice original `IX_ordem_servico_status` foi derrubado pela migration `20260914125322_HistoricoStatusEUnidade.cs:14-15` (`DropIndex(name: "IX_ordem_servico_status")`) e recriado como índice composto `ix_ordem_servico_status_data` em `(status, criada_em)` — ver "Ajustes complementares" abaixo |
| Índice em `os.cliente_id` | Sim | `OficinaDbContextModelSnapshot.cs:455` (`HasIndex("ClienteId")`, sem unicidade — mapeado como `IX_ordem_servico_cliente_id`) |
| `veiculo.placa` unique | Sim | `OficinaDbContextModelSnapshot.cs:555` (`OwnsOne("...Placa"...)`) e `:568-569` (`HasIndex("Valor").IsUnique()`, mapeado como `IX_veiculo_placa`) |
| `peca.sku` unique | Sim | `OficinaDbContextModelSnapshot.cs:592` (`OwnsOne("...Sku"...)`) e `:605-606` (`HasIndex("Valor").IsUnique()`, mapeado como `IX_peca_sku`) |
| `servico.nome` (índice) | Sim, não único | `OficinaDbContextModelSnapshot.cs:107` (`HasIndex("Nome")`, mapeado como `IX_servico_nome`) |
| `movimentacao (peca_id, criado_em)` | Sim | `OficinaDbContextModelSnapshot.cs:214-215` (`HasIndex("peca_id", "CriadoEm").HasDatabaseName("ix_movimentacao_peca_data")`) |
| Check em `movimentacao.quantidade` | Sim | `OficinaDbContextModelSnapshot.cs:219` (`HasCheckConstraint("ck_movimentacao_quantidade_positiva", "quantidade > 0")`) |

Todos os itens do diagnóstico original existem, com uma exceção parcial: o índice
isolado em `os.status` não sobrevive como tal — a Fase 3 o substitui pelo índice
composto descrito a seguir, porque o padrão de acesso real filtra por status **e**
ordena pela data de abertura (comentário em
`src/Oficina.Infraestrutura/Persistencia/Configuracoes/OrdemDeServicoConfiguration.cs:26-29`:
"Padrão de acesso real: filtrar por status e ordenar por data de abertura").

### A lacuna que motivou `os.historico_status`

Antes da Fase 3, `ordem_servico` guardava só o status atual e os timestamps de cada
marco (`diagnosticada_em`, `iniciada_em`, `finalizada_em` etc.), sem registro de
**quando** cada transição ocorreu nem de quanto tempo durou o status anterior — sem
isso, o painel "tempo médio de execução por status" exigido pela Fase 3 não tem como
ser calculado a partir do modelo. A tabela dedicada `os.historico_status`, sua
justificativa completa, sua escrita transacional e as duas correções de defeito que a
sustentam (duração sempre nula sem o `Include` do histórico; evento de criação nunca
emitido) estão em [ADR-020](../arquitetura/ADR-020-historico-status.md) — este RFC não
repete esse detalhamento, só referencia o resultado.

### Ajustes complementares (Fase 3)

- **Índice composto `(status, criada_em)`** em `os.ordem_servico`
  (`ix_ordem_servico_status_data`), substituindo o índice isolado em `status` —
  confirmado acima, na migration `20260914125322_HistoricoStatusEUnidade.cs:53-54,109-114`
  (que primeiro o cria, depois o próprio `Down()` o derruba e recria o índice antigo,
  espelhando a reversão) e em
  `OrdemDeServicoConfiguration.cs:28-29`. `ordem_servico` não possui coluna
  `atualizado_em`; `criada_em` é a data canônica usada na ordenação.
- **Checks de quantidade e valor** acrescentados a `os.item_peca` e `os.item_servico`
  na mesma migration: `ck_item_peca_quantidade_positiva`,
  `ck_item_servico_quantidade_positiva` (`quantidade > 0`) e
  `ck_item_peca_preco_nao_negativo`, `ck_item_servico_preco_nao_negativo`
  (`preco_snapshot >= 0`) — paridade com o check que `estoque.movimentacao` já tinha
  desde a Fase 2 (`OficinaDbContextModelSnapshot.cs:333-337,373-377`).
- **Coluna `unidade`** em `os.ordem_servico` (`character varying(60)`, default
  `'matriz'`, com índice `ix_ordem_servico_unidade`): o enunciado da Fase 3 descreve um
  cenário de múltiplas unidades, e sem essa coluna o dashboard não teria dimensão de
  segmentação por oficina para filtrar.

O schema completo — as 10 tabelas físicas, os 5 schemas, cada coluna com tipo físico e
nulidade, e a distinção entre FK física e referência lógica sem constraint — está em
[o diagrama entidade-relacionamento](../arquitetura/diagramas/entidade-relacionamento.md),
que é a fonte de verdade estrutural; este RFC descreve por que o schema tem esse
formato, não repete cada coluna.

### V15 — colunas que existem e nunca são preenchidas

`os.ordem_servico.unidade` e `os.historico_status.usuario_id` foram criadas para dar
suporte a segmentação por unidade e auditoria por usuário, mas **nenhum caso de uso as
preenche hoje**: `OrdemDeServico.Criar(Guid clienteId, Guid veiculoId, string?
observacoes = null, string unidade = "matriz")` tem `unidade` com valor padrão, e
nenhum chamador (`AbrirOrdemDeServicoUseCase`, `CriarOrdemUseCase`) o informa — toda OS
nasce `"matriz"`. `RegistrarTransicao` chama `HistoricoStatus.Criar` sem o parâmetro
opcional `usuarioId`, que fica sempre `NULL`
(`OrdemDeServicoConfiguration.cs:34-42`, comentário: "`unidade` NUNCA é populada com
outro valor (...) O mesmo vale para `historico_status.usuario_id`"). Alimentar as duas
exigiria origem da unidade (token, request ou configuração do pod) e propagação do
usuário autenticado até o domínio — trabalho fora do escopo desta fase, detalhado em
[ADR-020](../arquitetura/ADR-020-historico-status.md) e no
`oficina-app/README.md`, seção "Limitações conhecidas e decisões registradas", item 3.
