# ADR-026 — SonarQube efêmero no CI (SonarCloud opcional)

**Status:** Aceita
**Data:** 2026-11-17
**RFC relacionado:** [RFC-006](../rfc/RFC-006-microsservicos-saga-orquestrada.md)

## Contexto

A Fase 4 exige análise de qualidade com SonarQube (ou similar) no pipeline de cada serviço. O
caminho natural é o SonarCloud, mas ele depende de uma conta/organização externa e do secret
`SONAR_TOKEN` em cada repositório — bloqueio externo que não pode impedir o CI de rodar, nem
deixar a análise "pulada" em silêncio.

## Decisão

O job `sonar` do `.github/workflows/ci.yml` roda em **dois modos**, escolhidos pela presença do
secret `SONAR_TOKEN`:

| Modo | Quando | Como |
|---|---|---|
| **SonarQube Community efêmero** | `SONAR_TOKEN` ausente (estado atual) | *service container* `sonarqube:26.10.0.133078-community` no próprio job: o passo provisiona senha, token e projeto pela API, roda `dotnet-sonarscanner` 12.1.0 com cobertura OpenCover (Coverlet) e resultados `.trx`, espera o processamento, **reprova o job se o quality gate for `ERROR`** e publica um resumo (quality gate, cobertura, testes, bugs, vulnerabilidades, hotspots, code smells, duplicação, ratings e issues BUG/VULNERABILITY) no *job summary* e no artefato `sonar-summary` |
| **SonarCloud** | `SONAR_TOKEN` presente | mesma análise contra `https://sonarcloud.io` (organização `vars.SONAR_ORGANIZATION`, padrão = dono do repositório; chave `<dono>_<repositório>`), com `sonar.qualitygate.wait=true` |

Comum aos dois modos: exclusões `terraform/**`, `k8s/**`, `docker/**`, `**/Migrations/**`;
`tests/**` fora da cobertura; a regra `csharpsquid:S2068` ignorada só em `**/appsettings.json`
(a única "senha" ali é a connection string de desenvolvimento local). O bloco é portável:
chave e nome do projeto vêm de `github.event.repository.name`; o Billing e a Execução têm um
job `sonar` no próprio CI.

## Alternativas

- **Só SonarCloud, pulando a análise sem token** — o CI ficaria verde sem análise nenhuma.
- **SonarQube permanente no cluster** — mais um serviço com estado para operar.

## Consequências

- ✅ Análise real com quality gate em todo push/PR, sem conta externa.
- ✅ Evidência navegável por execução (*job summary* + artefato `sonar-summary`).
- ⚠️ O SonarQube efêmero não guarda histórico entre execuções nem analisa o *new code* de forma
  incremental: cada análise é completa. O histórico aparece quando o `SONAR_TOKEN` for
  configurado (SonarCloud).
- ⚠️ O *service container* sobe mesmo no modo SonarCloud (o GitHub Actions não aceita o
  contexto `secrets` em `services`), custando alguns segundos de job.
