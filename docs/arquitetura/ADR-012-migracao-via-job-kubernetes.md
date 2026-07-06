# ADR-012 — Migração de banco via Job do Kubernetes

**Status:** Aceita
**Data:** 2026-07-06

## Contexto

Localmente (docker-compose) a API aplica migrations e faz o bootstrap do admin no
startup. Com **múltiplas réplicas** no Kubernetes, deixar cada pod migrar no startup
causaria **corrida** entre réplicas (várias tentando migrar/bootstrapar ao mesmo tempo)
e acoplaria o tempo de subida da API à migração.

## Decisão

Separar a migração da subida da API:

- A API suporta um **modo Job**: `dotnet Oficina.Api.dll migrate` (ou `STARTUP_TASK=migrate`).
  Nesse modo ela aplica migrations + bootstrap e **encerra sem subir o servidor web**
  (`Program.cs` roda o `IInicializadorBanco` e retorna antes de `app.Run()`).
- Um **Job** do Kubernetes (`k8s/migration-job.yaml`, `oficina-migrate`) executa esse modo
  **uma única vez** antes do rollout. O CD faz `kubectl wait --for=condition=complete`
  no Job antes de aplicar o Deployment.
- Os pods do Deployment **não** migram: o `configmap.yaml` define
  `Bootstrap__ExecutarNoStartup=false` e o `BootstrapAdminHostedService` respeita a flag
  (`InicializadorBanco.DeveExecutarNoStartup`). No local, a ausência da chave mantém a
  migração no startup por conveniência.

## Consequências

- ✅ Sem corrida entre réplicas — a migração roda uma vez, isolada
- ✅ Schema garantido antes de qualquer pod da API atender tráfego
- ✅ Mesmo binário serve API e migração (sem imagem separada)
- ⚠️ O Job é imutável: o CD faz `kubectl delete job` antes de reaplicar em novos deploys
- ⚠️ Migrations devem ser retrocompatíveis (a versão antiga pode conviver com o schema novo durante o rollout)
