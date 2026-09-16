# ADR-013 — Observabilidade mínima (OpenTelemetry /metrics)

**Status:** Aceita
**Data:** 2026-07-06

> **Nota de 2026-09-16 (Fase 3).** A métrica de memória do HPA (`averageUtilization` sobre
> `requests.memory`) foi **removida** nesta fase, por um motivo medido: ver
> [ADR-017](ADR-017-escalabilidade-hpa.md). O restante deste ADR continua valendo — Serilog,
> métricas OpenTelemetry em `/metrics` e o HPA escalando por CPU via `metrics-server` — e o
> status permanece **Aceita**; este ADR não foi superseded.

## Contexto

A Fase 2 valoriza resiliência e operabilidade, mas é um MVP acadêmico com custo e tempo
limitados. Uma stack completa de observabilidade (Prometheus + Grafana + tracing +
log aggregation) seria desproporcional; ainda assim, o HPA e a demonstração de
escalabilidade se beneficiam de métricas expostas.

## Decisão

Adotar **observabilidade mínima**:

- **Logs** estruturados em JSON via **Serilog** para stdout (coletáveis pelo Kubernetes).
- **Métricas** via **OpenTelemetry** expostas em **`/metrics`** no formato Prometheus
  (`AddPrometheusExporter` + `MapPrometheusScrapingEndpoint`), instrumentando requisições
  ASP.NET Core e o runtime .NET. Endpoint anônimo (scraping).
- O **HPA** escala por métricas de CPU/memória via **metrics-server** (não depende de
  um Prometheus instalado).

Não instalamos Prometheus/Grafana/tracing distribuído no cluster nesta fase.

## Consequências

- ✅ Métricas prontas para scraping sem infra adicional; logs estruturados prontos para agregação
- ✅ HPA funcional com metrics-server (custo baixo)
- ✅ Caminho de evolução claro: apontar um Prometheus para `/metrics` quando necessário
- ⚠️ Sem dashboards/alertas/tracing prontos — observação é manual (kubectl, logs, curl /metrics)
- ⚠️ `/metrics` anônimo: em produção real, restringir por rede/authn
