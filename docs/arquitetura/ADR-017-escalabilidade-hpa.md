# ADR-017 — Escalabilidade horizontal via HPA

**Status:** Aceita
**Data:** 2026-09-15

## Contexto

O design arquitetural da Fase 3 (`fase3-design-arquitetural.md`, seção 3, item 6) previa
manter o HPA da Fase 2 sem alteração: 2–10 réplicas, gatilho de CPU a 70%. Esse documento é
o registro histórico aprovado antes da execução e foi deixado intocado de propósito — este
ADR é quem corrige o valor real, conforme a Constraint 3 do plano manda fazer sempre que a
execução diverge do design.

## Decisão

O `k8s/hpa.yaml` real (`HorizontalPodAutoscaler`, `autoscaling/v2`) escala o `Deployment`
`oficina-api` entre **`minReplicas: 2`** e **`maxReplicas: 10`**, com uma única métrica: CPU,
`averageUtilization: 60`. **O alvo de CPU é 60%, não os 70% do design.**

Havia também uma métrica de memória, com `averageUtilization: 70` sobre `requests.memory`, e
ela foi **removida**. O motivo, medido, não hipotético: `requests.memory` do container é
`256Mi` (`k8s/deployment.yaml`), o que faz um alvo de 70% equivaler a cerca de 179Mi por pod;
com o agente do New Relic ligado (`CORECLR_ENABLE_PROFILING=1`, adotado nesta fase — ver
[RFC-005](../rfc/RFC-005-observabilidade-new-relic.md)), o pod em repouso, sem atender
nenhuma requisição, já consome cerca de 215Mi de RSS — acima do alvo de memória antes de
qualquer tráfego chegar. Como esse consumo é por pod e não dilui ao replicar (cada réplica
nova nasce igualmente perto do limite), o HPA calculava o percentual de memória já estourado
em qualquer número de réplicas e escalava direto até `maxReplicas`, ficando preso lá — pods
ociosos reservando `256Mi` de request cada, sem que o autoscaler pudesse descer. Reintroduzir
essa métrica exige primeiro medir o consumo de repouso com o profiler ligado e então subir o
`averageUtilization` ou o `requests.memory` para acomodá-lo — nenhum dos dois foi ajustado
nesta fase.

O HPA existe nos **dois** namespaces (`oficina-hml` e `oficina-prd`), um recurso por
ambiente, e depende do **`metrics-server`** instalado via Helm pelo Terraform do
`oficina-infra-k8s`: sem ele as métricas de CPU ficam `<unknown>` e o HPA não escala. O
node group do EKS opera com **2 nós `t3.medium`** — o teto real de réplicas que os nós
comportam é limitado por essa capacidade, independentemente de `maxReplicas: 10` permitir
mais.

## Consequências

- ✅ Escala por demanda com um alvo de CPU que reflete consumo medido, não um valor herdado
  sem verificação
- ✅ Sem depender de Prometheus ou de outra fonte de métricas além do `metrics-server`
- ⚠️ Escalar só por CPU não cobre saturação por I/O de banco (conexões Npgsql esgotadas, por
  exemplo, sem CPU alta correspondente)
- ⚠️ `maxReplicas: 10` é um teto teórico: com 2 nós `t3.medium`, a capacidade real de pods
  agendáveis é menor do que esse número sugere
- ⚠️ Reintroduzir a métrica de memória exige medir antes o consumo em repouso com o profiler
  do New Relic ligado e ajustar `requests.memory` ou o `averageUtilization` para acomodá-lo —
  do contrário o mesmo travamento em `maxReplicas` se repete
