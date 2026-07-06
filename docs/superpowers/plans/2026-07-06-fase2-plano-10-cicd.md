# Fase 2 — Plano 10: CI/CD (GitHub Actions)

> **For agentic workers:** REQUIRED SUB-SKILL: `superpowers:subagent-driven-development`. Steps usam checkbox (`- [ ]`). É **UMA Task** (um único commit). Este plano SÓ **escreve/edita arquivos** em `.github/workflows/` — **nada de C# muda**. Os workflows **rodam no GitHub, não localmente**: a validação local é apenas (a) **estática** (greps de segurança/lógica + parse YAML com PyYAML) e (b) `dotnet build Oficina.sln` **inalterado (0 erros)**. A execução real do `ci.yml` acontece no push/PR; a do `cd.yml`/`infra.yml` acontece quando a pessoa faz push em `main` ou dispara o `workflow_dispatch` com a infra AWS (Plano 09) já provisionada.

**Goal:** Entregar o pipeline CI/CD da Fase 2 com **três** workflows do GitHub Actions:
1. `ci.yml` **evoluído** — continua rodando em `push`/`pull_request`, agora também na branch `fase-2`, com build Release, `dotnet test` da solution inteira (inclui os testes de integração Testcontainers e os projetos novos `Oficina.Adaptadores.Testes`/`Oficina.Infraestrutura.Testes`), **gate de cobertura 80%**, scan de pacotes vulneráveis, build da imagem Docker (valida o Dockerfile do Plano 07) e CodeQL. Ganha um job **advisory** de `dotnet format` (qualidade, Módulo 3 — Aula 4).
2. `cd.yml` **novo** — deploy contínuo no **EKS** apenas em `main` (+ `workflow_dispatch`), via **OIDC** (sem chave estática): login no ECR, build/push da imagem taggeada pelo `github.sha`, criação idempotente do Secret do K8s a partir de GitHub Secrets, `envsubst` da imagem nos manifestos, Job de migração com `kubectl wait`, e rollout do Deployment.
3. `infra.yml` **novo** — Terraform manual (`workflow_dispatch` com `plan`/`apply`/`destroy`) via OIDC (role de infra separada). Conveniência para re-apply/destroy; o provisionamento **inicial** normalmente é local (chicken-and-egg do role admin).

**Architecture:** O CI é o portão de qualidade em todo push/PR. O CD assume a infra do Plano 09 (`infra/`) pronta e os manifestos do Plano 08 (`k8s/`) como fonte da verdade da orquestração — ele apenas **substitui a imagem** (`${ECR_REPOSITORY}:${IMAGE_TAG}` → `envsubst`) e **injeta os segredos reais** (o `k8s/secret.yaml` é só um template). A autenticação AWS é **100% OIDC** (`aws-actions/configure-aws-credentials@v4` assumindo o `github_actions_role_arn` do Terraform), sem `aws-access-key-id`/`aws-secret-access-key`. Escopar o CD a `main` limita o uso do role OIDC a essa branch (hardening alinhado ao trust `repo:...:*` do Plano 09; pode-se apertar o trust para `ref:refs/heads/main` depois). O `infra.yml` usa um role **separado** de permissões amplas (`AWS_TERRAFORM_ROLE_ARN`).

**Tech Stack:** GitHub Actions (runners `ubuntu-latest`, que trazem **Docker em execução**, `kubectl`, `envsubst`/`gettext-base` e AWS CLI v2 pré-instalados). Actions: `actions/checkout@v4`, `actions/setup-dotnet@v4`, `actions/upload-artifact@v4`, `github/codeql-action/*@v3`, `aws-actions/configure-aws-credentials@v4`, `aws-actions/amazon-ecr-login@v2`, `hashicorp/setup-terraform@v3`. .NET 8 SDK, `dotnet-reportgenerator-globaltool`, `dotnet format`. Terraform (via setup-terraform). Alvo do CD: EKS + ECR + RDS (Plano 09), manifestos `k8s/` (Plano 08).

## Global Constraints

- **Idioma pt-BR** em comentários dos workflows e na mensagem de commit.
- **Actions pinadas por major** (`@v4`/`@v3`/`@v2`) — consistente com o `dependabot.yml` (ecossistema `github-actions`) que abre PRs de atualização.
- **Sem segredo hardcoded; nada de `echo` de secret.** Segredos entram via `${{ secrets.* }}`, preferencialmente por bloco `env:` no step, e são consumidos por `--from-literal`/`TF_VAR_*`. O GitHub mascara valores de secrets no log; ainda assim **nunca** fazer `echo`/`cat`/`set -x` sobre eles.
- **OIDC obrigatório** (`permissions: id-token: write` + `contents: read`), **sem** `aws-access-key-id`/`aws-secret-access-key` em lugar algum.
- **Workflows validados no GitHub, não localmente.** O gate LOCAL é: arquivos escritos + verificações estáticas (Step 5) + `dotnet build Oficina.sln` **0 erros** (nada de C# muda).
- **Branch:** `fase-2` (sem merge/push). **Bash tool = Git Bash.** **UM commit** (uma Task), mensagem pt-BR terminando com:
  `Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>`

## Estrutura de arquivos

```
.github/workflows/ci.yml      MODIFICADO  (trigger fase-2 + job advisory dotnet format; demais jobs mantidos)
.github/workflows/cd.yml      NOVO        (deploy EKS via OIDC — push main + workflow_dispatch)
.github/workflows/infra.yml   NOVO        (Terraform manual via OIDC — workflow_dispatch plan/apply/destroy)
```

3 arquivos (1 modificado, 2 novos), todos sob `.github/workflows/`. Nenhum código C# é tocado. `.github/dependabot.yml` **não muda** (já cobre nuget/actions/docker).

> **Secrets/Variables do GitHub** que os workflows consomem estão consolidados na seção **"Secrets e Variables do GitHub"** ao final (o README consolidado é o Plano 12). Definir em *Settings → Secrets and variables → Actions* **antes** de rodar `cd.yml`/`infra.yml`.

---

## Task 1: Três workflows de CI/CD (`.github/workflows/` — validação estática)

**Files:** modificar `.github/workflows/ci.yml`; criar `.github/workflows/cd.yml` e `.github/workflows/infra.yml`.

> **Sem execução local.** Cada YAML é validado por parse (PyYAML) + `grep` de segurança/lógica (Step 5) e pelo gate `dotnet build` (Step 5.13). A execução real ocorre no GitHub (push/PR para o CI; push em `main`/dispatch para CD; dispatch para infra).

- [ ] **Step 1: `.github/workflows/ci.yml`** — versão **evoluída completa**. Adiciona `fase-2` aos triggers e o job **advisory** `format`; mantém `build-test` (com gate 80%), `docker-build` e `codeql` intactos. Sobrescrever o arquivo inteiro com o conteúdo abaixo:

  ```yaml
  name: CI

  on:
    push:
      # Plano 10: adiciona fase-2 (mantendo main/develop) para o CI rodar na branch de trabalho.
      branches: [main, develop, fase-2]
    pull_request:
      branches: [main, develop, fase-2]

  jobs:
    build-test:
      runs-on: ubuntu-latest
      # ubuntu-latest tem Docker EM EXECUCAO -> os testes de integracao (Testcontainers.PostgreSql),
      # que nao rodam localmente, sao exercitados AQUI. O `dotnet test` roda a solution inteira,
      # incluindo os projetos novos Oficina.Adaptadores.Testes e Oficina.Infraestrutura.Testes.
      steps:
        - uses: actions/checkout@v4

        - name: Setup .NET 8
          uses: actions/setup-dotnet@v4
          with:
            dotnet-version: 8.0.x

        - name: Restore
          run: dotnet restore

        - name: Build
          run: dotnet build --no-restore --configuration Release

        - name: Test with coverage
          run: dotnet test --no-build --configuration Release --collect:"XPlat Code Coverage" --settings coverlet.runsettings --results-directory ./TestResults

        - name: Install ReportGenerator
          run: dotnet tool install -g dotnet-reportgenerator-globaltool

        - name: Generate coverage report
          run: |
            reportgenerator \
              -reports:"./TestResults/**/coverage.cobertura.xml" \
              -targetdir:"./TestResults/CoverageReport" \
              -reporttypes:"HtmlInline;TextSummary;Cobertura"

        - name: Show coverage summary
          run: cat ./TestResults/CoverageReport/Summary.txt

        - name: Enforce minimum coverage (80%)
          run: |
            line_rate=$(grep -oP 'Line coverage:\s*\K[0-9.]+' ./TestResults/CoverageReport/Summary.txt | head -1)
            echo "Coverage: $line_rate%"
            awk -v cov="$line_rate" 'BEGIN { if (cov+0 < 80) { print "FAIL: coverage below 80%"; exit 1 } else print "OK: coverage >= 80%" }'

        - name: Upload coverage report
          uses: actions/upload-artifact@v4
          with:
            name: coverage-report
            path: ./TestResults/CoverageReport
            if-no-files-found: error

        - name: List vulnerable packages
          run: dotnet list package --vulnerable --include-transitive || true

    format:
      # Qualidade de codigo (Modulo 3 - Aula 4): checagem de estilo com `dotnet format`.
      # ADVISORY / NAO-BLOQUEANTE: `continue-on-error: true` evita quebrar o CI por estilo
      # pre-existente. TODO: quando o codigo estiver 100% formatado, remover o continue-on-error
      # (ou trocar --verify-no-changes por gate real) para torna-lo bloqueante.
      runs-on: ubuntu-latest
      steps:
        - uses: actions/checkout@v4

        - name: Setup .NET 8
          uses: actions/setup-dotnet@v4
          with:
            dotnet-version: 8.0.x

        - name: Restore
          run: dotnet restore

        - name: dotnet format (verificacao — advisory)
          continue-on-error: true
          run: dotnet format Oficina.sln --verify-no-changes --no-restore --verbosity diagnostic

    docker-build:
      runs-on: ubuntu-latest
      steps:
        - uses: actions/checkout@v4
        - name: Build Docker image
          # Valida o Dockerfile do Plano 07 (agora inclui Oficina.Adaptadores no restore/publish).
          run: docker build -f docker/Dockerfile -t oficina-api:ci .

    codeql:
      runs-on: ubuntu-latest
      permissions:
        actions: read
        contents: read
        security-events: write
      steps:
        - uses: actions/checkout@v4
        - uses: github/codeql-action/init@v3
          with:
            languages: csharp
            build-mode: autobuild
        - uses: github/codeql-action/analyze@v3
          with:
            category: "/language:csharp"
  ```

- [ ] **Step 2: `.github/workflows/cd.yml`** — deploy contínuo no EKS. **Só em `main`** (+ `workflow_dispatch`), OIDC, sem chave estática. Criar o arquivo com o conteúdo abaixo:

  ```yaml
  name: CD

  on:
    push:
      # Deploy SO em main -> escopa o uso do role OIDC a main (hardening do Plano 09).
      branches: [main]
    workflow_dispatch: {}

  # OIDC: id-token para assumir o role AWS; contents apenas leitura do repo.
  permissions:
    id-token: write
    contents: read

  # Evita dois deploys simultaneos na mesma branch (o mais novo cancela o anterior).
  concurrency:
    group: cd-${{ github.ref }}
    cancel-in-progress: false

  jobs:
    deploy:
      runs-on: ubuntu-latest
      # ubuntu-latest ja traz Docker, AWS CLI v2, kubectl e envsubst (gettext-base) pre-instalados.
      env:
        # Variaveis NAO sensiveis (GitHub Variables). ECR_REPOSITORY = URL completa do repo ECR
        # (output ecr_repository_url do Terraform, ex.: 123456789012.dkr.ecr.us-east-1.amazonaws.com/oficina-api).
        AWS_REGION: ${{ vars.AWS_REGION }}
        ECR_REPOSITORY: ${{ vars.ECR_REPOSITORY }}
        # Tag da imagem = SHA do commit (imutavel, rastreavel).
        IMAGE_TAG: ${{ github.sha }}
      steps:
        - uses: actions/checkout@v4

        # 1) Credenciais AWS via OIDC (SEM aws-access-key-id/secret): assume o role do Terraform.
        - name: Configurar credenciais AWS (OIDC)
          uses: aws-actions/configure-aws-credentials@v4
          with:
            role-to-assume: ${{ secrets.AWS_ROLE_ARN }}
            aws-region: ${{ vars.AWS_REGION }}

        # 2) Login no ECR (usa as credenciais temporarias do passo anterior).
        - name: Login no Amazon ECR
          uses: aws-actions/amazon-ecr-login@v2

        # 3) Build + push da imagem (tag = SHA; tambem 'latest' por conveniencia).
        - name: Build e push da imagem no ECR
          run: |
            docker build -f docker/Dockerfile -t "$ECR_REPOSITORY:$IMAGE_TAG" -t "$ECR_REPOSITORY:latest" .
            docker push "$ECR_REPOSITORY:$IMAGE_TAG"
            docker push "$ECR_REPOSITORY:latest"

        # 4) kubeconfig apontando para o cluster EKS (Plano 09).
        - name: Configurar kubeconfig do EKS
          run: aws eks update-kubeconfig --name "${{ vars.EKS_CLUSTER_NAME }}" --region "$AWS_REGION"

        # 5) Namespace + ConfigMap (config nao-sensivel). Namespace PRIMEIRO (o Secret depende dele).
        - name: Aplicar Namespace e ConfigMap
          run: |
            kubectl apply -f k8s/namespace.yaml
            kubectl apply -f k8s/configmap.yaml

        # 6) Secret REAL a partir de GitHub Secrets (o k8s/secret.yaml e apenas template, NAO e aplicado).
        #    Segredos vem por env mascarada; NUNCA sao ecoados. `create --dry-run=client -o yaml | apply`
        #    = upsert idempotente (funciona no 1o deploy e nos seguintes).
        - name: Criar/atualizar Secret do K8s
          env:
            JWT_SECRET: ${{ secrets.JWT_SECRET }}
            DB_USER: ${{ secrets.DB_USER }}
            DB_PASSWORD: ${{ secrets.DB_PASSWORD }}
            RDS_ENDPOINT: ${{ secrets.RDS_ENDPOINT }}
            ADMIN_BOOTSTRAP_PASSWORD: ${{ secrets.ADMIN_BOOTSTRAP_PASSWORD }}
            WEBHOOK_TOKEN: ${{ secrets.WEBHOOK_TOKEN }}
          run: |
            # Monta a connection string a partir do endpoint do RDS + credenciais (nunca ecoada).
            CONN="Host=${RDS_ENDPOINT};Port=5432;Database=oficina;Username=${DB_USER};Password=${DB_PASSWORD}"
            kubectl create secret generic oficina-api-secret \
              --namespace oficina \
              --from-literal=Jwt__Secret="${JWT_SECRET}" \
              --from-literal=ConnectionStrings__Default="${CONN}" \
              --from-literal=AdminBootstrap__Password="${ADMIN_BOOTSTRAP_PASSWORD}" \
              --from-literal=Webhook__Token="${WEBHOOK_TOKEN}" \
              --dry-run=client -o yaml | kubectl apply -f -

        # 7) Job de migracao (migra + bootstrap). O Job e imutavel: deletar antes de re-aplicar.
        #    envsubst substitui SO ${ECR_REPOSITORY}/${IMAGE_TAG} (nao mexe em outros '$').
        #    Aguarda a conclusao ANTES do rollout (evita subir a API sem o schema migrado).
        - name: Rodar Job de migracao e aguardar conclusao
          run: |
            kubectl delete job oficina-migrate -n oficina --ignore-not-found
            envsubst '${ECR_REPOSITORY} ${IMAGE_TAG}' < k8s/migration-job.yaml | kubectl apply -f -
            kubectl wait --for=condition=complete job/oficina-migrate -n oficina --timeout=300s

        # 8) Deployment (imagem substituida por envsubst) + Service + HPA.
        - name: Aplicar Deployment, Service e HPA
          run: |
            envsubst '${ECR_REPOSITORY} ${IMAGE_TAG}' < k8s/deployment.yaml | kubectl apply -f -
            kubectl apply -f k8s/service.yaml
            kubectl apply -f k8s/hpa.yaml

        # 9) Aguarda o rollout terminar (falha o deploy se os pods novos nao ficarem prontos).
        - name: Aguardar rollout
          run: kubectl rollout status deployment/oficina-api -n oficina --timeout=300s
  ```

- [ ] **Step 3: `.github/workflows/infra.yml`** — Terraform **manual** via OIDC. Criar o arquivo com o conteúdo abaixo:

  ```yaml
  # ==============================================================================================
  # Terraform MANUAL (conveniencia). ATENCAO ao chicken-and-egg:
  #   O role OIDC de INFRA (AWS_TERRAFORM_ROLE_ARN) precisa de permissoes AMPLAS (admin) para
  #   criar VPC/EKS/RDS/IAM. Esse role NAO existe antes do primeiro `terraform apply` e criar um
  #   role admin exige... um admin. Por isso o PROVISIONAMENTO INICIAL normalmente e LOCAL
  #   (`cd infra && terraform init/apply`, ver infra/README.md do Plano 09). Este workflow e
  #   conveniencia para re-apply/destroy QUANDO JA EXISTIR um role de infra admin (criado a mao
  #   ou por um bootstrap separado). A spec permite "workflow manual OU Terraform local" — se
  #   preferir, ignore este workflow e use o Terraform local.
  # ==============================================================================================
  name: Infra (Terraform manual)

  on:
    workflow_dispatch:
      inputs:
        acao:
          description: "Acao do Terraform a executar"
          required: true
          default: plan
          type: choice
          options:
            - plan
            - apply
            - destroy

  # OIDC (sem chave estatica).
  permissions:
    id-token: write
    contents: read

  jobs:
    terraform:
      runs-on: ubuntu-latest
      env:
        # Variaveis do Terraform (TF_VAR_*). db_password e SENSIVEL -> vem de secret, nunca ecoada.
        TF_VAR_db_password: ${{ secrets.DB_PASSWORD }}
        TF_VAR_db_username: ${{ secrets.DB_USER }}
        TF_VAR_region: ${{ vars.AWS_REGION }}
      steps:
        - uses: actions/checkout@v4

        # OIDC com o role de INFRA (admin), SEPARADO do role de deploy (AWS_ROLE_ARN).
        - name: Configurar credenciais AWS (OIDC — role de infra)
          uses: aws-actions/configure-aws-credentials@v4
          with:
            role-to-assume: ${{ secrets.AWS_TERRAFORM_ROLE_ARN }}
            aws-region: ${{ vars.AWS_REGION }}

        - name: Setup Terraform
          uses: hashicorp/setup-terraform@v3

        # init conecta ao backend S3+DynamoDB (Plano 09). -chdir aponta para infra/.
        - name: Terraform init
          run: terraform -chdir=infra init

        - name: Terraform plan
          if: ${{ inputs.acao == 'plan' }}
          run: terraform -chdir=infra plan

        - name: Terraform apply
          if: ${{ inputs.acao == 'apply' }}
          run: terraform -chdir=infra apply -auto-approve

        - name: Terraform destroy
          if: ${{ inputs.acao == 'destroy' }}
          run: terraform -chdir=infra destroy -auto-approve
  ```

- [ ] **Step 4: Secrets/Variables do GitHub — documentar** (nada a escrever em arquivo aqui; conferir a seção **"Secrets e Variables do GitHub"** abaixo e garantir que os `${{ secrets.* }}`/`${{ vars.* }}` referenciados nos três YAML batem exatamente com os nomes daquela lista). A configuração real é em *Settings → Secrets and variables → Actions*.

- [ ] **Step 5: Verificações estáticas (Git Bash, sem GitHub) + gate `dotnet build`.**

  ```bash
  cd "C:/Users/thiago/source/repos/Fiap/fiap_15SOAT_fase1"

  # 0) Os tres arquivos existem
  ls -1 .github/workflows/ci.yml .github/workflows/cd.yml .github/workflows/infra.yml

  # 1) Trigger fase-2 no CI (push e pull_request)
  grep -nE 'branches:\s*\[main, develop, fase-2\]' .github/workflows/ci.yml
  #    (esperado: 2 linhas — push e pull_request)

  # 2) Job advisory de dotnet format presente e NAO-bloqueante
  grep -n 'dotnet format' .github/workflows/ci.yml
  grep -n 'continue-on-error: true' .github/workflows/ci.yml

  # 3) CD dispara SO em main (+ workflow_dispatch) — nao pode ter develop/fase-2 no trigger do CD
  grep -nA3 '^on:' .github/workflows/cd.yml
  grep -n 'branches: \[main\]' .github/workflows/cd.yml
  grep -nE 'develop|fase-2' .github/workflows/cd.yml && echo "FALHA: CD nao pode disparar fora de main" || echo "OK: CD so em main"

  # 4) OIDC: id-token write em cd.yml e infra.yml
  grep -n 'id-token: write' .github/workflows/cd.yml .github/workflows/infra.yml
  grep -n 'contents: read' .github/workflows/cd.yml .github/workflows/infra.yml

  # 5) OIDC SEM chave estatica: NENHUM aws-access-key-id / aws-secret-access-key
  grep -rniE 'aws-access-key-id|aws-secret-access-key|AKIA[0-9A-Z]{16}' .github/workflows/ \
    && echo "FALHA: credencial estatica encontrada" || echo "OK: sem credencial estatica (so OIDC)"

  # 6) NENHUM echo/print de secret (nao pode `echo ... secrets.` nem `set -x`)
  grep -rnE 'echo[^\n]*secrets\.' .github/workflows/ && echo "FALHA: echo de secret" || echo "OK: sem echo de secret"
  grep -rn 'set -x' .github/workflows/ && echo "FALHA: set -x pode vazar segredo" || echo "OK: sem set -x"

  # 7) configure-aws-credentials assume role via secret (deploy e infra)
  grep -n 'role-to-assume: ${{ secrets.AWS_ROLE_ARN }}' .github/workflows/cd.yml
  grep -n 'role-to-assume: ${{ secrets.AWS_TERRAFORM_ROLE_ARN }}' .github/workflows/infra.yml

  # 8) CD: Secret criado por --from-literal + dry-run|apply (upsert), sem template do secret.yaml
  grep -n 'kubectl create secret generic oficina-api-secret' .github/workflows/cd.yml
  grep -n -- '--dry-run=client -o yaml | kubectl apply -f -' .github/workflows/cd.yml
  grep -n 'k8s/secret.yaml' .github/workflows/cd.yml && echo "FALHA: nao aplicar o template secret.yaml" || echo "OK: secret.yaml (template) nao e aplicado"

  # 9) CD: envsubst restrito a imagem + wait do Job antes do deployment
  grep -n "envsubst '\${ECR_REPOSITORY} \${IMAGE_TAG}'" .github/workflows/cd.yml
  grep -n 'kubectl wait --for=condition=complete job/oficina-migrate' .github/workflows/cd.yml
  grep -n 'kubectl delete job oficina-migrate -n oficina --ignore-not-found' .github/workflows/cd.yml
  grep -n 'kubectl rollout status deployment/oficina-api' .github/workflows/cd.yml

  # 10) Actions pinadas por major (@v2/@v3/@v4) — inspecao rapida
  grep -rnE 'uses: .*@v[0-9]+' .github/workflows/

  # 11) infra.yml: input acao com plan/apply/destroy + TF_VAR_db_password via secret
  grep -nE 'plan|apply|destroy' .github/workflows/infra.yml
  grep -n 'TF_VAR_db_password: ${{ secrets.DB_PASSWORD }}' .github/workflows/infra.yml

  # 12) Parse YAML com PyYAML (se disponivel). Nota: a chave `on:` vira booleano True no YAML 1.1
  #     (gotcha conhecido) — isso NAO e erro de sintaxe; o parse ainda valida a estrutura.
  python - <<'PY'
  import sys, glob, yaml
  ok = True
  for f in sorted(glob.glob('.github/workflows/*.yml')):
      try:
          yaml.safe_load(open(f, encoding='utf-8'))
          print('YAML OK  :', f)
      except Exception as e:
          ok = False
          print('YAML FALHOU:', f, '->', e)
  sys.exit(0 if ok else 1)
  PY

  # 13) GATE: build C# inalterado (nada de C# muda; so YAML de workflow)
  dotnet build Oficina.sln
  ```

  Checklist manual esperado:
  - 3 arquivos presentes; `ci.yml` com `[main, develop, fase-2]` em push **e** pull_request; job `format` com `continue-on-error: true`. ✔
  - `cd.yml` dispara **só em main** (+ `workflow_dispatch`), **sem** develop/fase-2. ✔
  - `id-token: write` + `contents: read` em `cd.yml` e `infra.yml`; **zero** `aws-access-key-id`/`aws-secret-access-key`/`AKIA…`. ✔
  - **Nenhum** `echo` de `secrets.*` nem `set -x`. ✔
  - `role-to-assume` = `AWS_ROLE_ARN` (CD) e `AWS_TERRAFORM_ROLE_ARN` (infra). ✔
  - CD cria o Secret por `--from-literal` + `--dry-run=client -o yaml | kubectl apply -f -` e **não** aplica o `k8s/secret.yaml` (template). ✔
  - `envsubst` restrito a `${ECR_REPOSITORY} ${IMAGE_TAG}`; `kubectl wait` do Job **antes** do deployment; `rollout status` no fim. ✔
  - Actions pinadas por major; `infra.yml` com input `acao` (`plan`/`apply`/`destroy`) e `TF_VAR_db_password` via secret. ✔
  - PyYAML parseia os 3 arquivos sem erro; `dotnet build Oficina.sln` **0 erros**. ✔

- [ ] **Step 6: commit único da Task** (mensagem pt-BR).

  ```bash
  cd "C:/Users/thiago/source/repos/Fiap/fiap_15SOAT_fase1"
  git add .github/workflows/ci.yml .github/workflows/cd.yml .github/workflows/infra.yml
  git commit -m "$(cat <<'EOF'
  feat(cicd): pipeline CI/CD Fase 2 (CI evoluido + CD EKS + infra manual)

  Evolui .github/workflows/ci.yml: adiciona a branch fase-2 aos triggers de push/PR e um
  job advisory de `dotnet format --verify-no-changes` (nao-bloqueante, continue-on-error);
  mantem build Release, dotnet test da solution (inclui integracao Testcontainers e os
  projetos novos Adaptadores/Infraestrutura), gate de cobertura 80%, scan de pacotes
  vulneraveis, docker-build e CodeQL.

  Adiciona .github/workflows/cd.yml: deploy contInuo no EKS SO em main (+ workflow_dispatch)
  via OIDC (sem chave estatica) — login no ECR, build/push da imagem com tag=SHA, Secret do
  K8s criado a partir de GitHub Secrets (upsert idempotente, sem ecoar segredo), envsubst da
  imagem nos manifestos, Job de migracao com kubectl wait antes do rollout do Deployment.

  Adiciona .github/workflows/infra.yml: Terraform manual (workflow_dispatch plan/apply/destroy)
  via OIDC com role de infra separado; documenta o chicken-and-egg (provisionamento inicial
  normalmente local). Nada de C# muda.

  Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
  EOF
  )"
  ```

---

## Secrets e Variables do GitHub

Configurar em *Settings → Secrets and variables → Actions* **antes** de rodar `cd.yml`/`infra.yml`. (Consolidação final no README é o Plano 12.)

### Secrets (sensíveis — nunca ecoados)

| Secret | Usado em | Origem / valor | Observação |
|---|---|---|---|
| `AWS_ROLE_ARN` | `cd.yml` | output `github_actions_role_arn` do Terraform (Plano 09) | Role OIDC de **deploy** (ECR push + EKS describe/access entry). |
| `AWS_TERRAFORM_ROLE_ARN` | `infra.yml` | role admin criado à mão/local (opcional) | **Opcional** — só se usar o `infra.yml`; role de permissões amplas. |
| `DB_USER` | `cd.yml`, `infra.yml` | `db_username` (RDS) | Compõe `ConnectionStrings__Default` (CD) e `TF_VAR_db_username` (infra). |
| `DB_PASSWORD` | `cd.yml`, `infra.yml` | senha do RDS | `ConnectionStrings__Default` (CD) e `TF_VAR_db_password` (infra). |
| `RDS_ENDPOINT` | `cd.yml` | output `rds_endpoint` do Terraform | Host do `ConnectionStrings__Default`. |
| `JWT_SECRET` | `cd.yml` | ≥64 chars (HS256) | Secret `Jwt__Secret`. |
| `ADMIN_BOOTSTRAP_PASSWORD` | `cd.yml` | senha do admin de bootstrap | Secret `AdminBootstrap__Password`. |
| `WEBHOOK_TOKEN` | `cd.yml` | token do webhook | Secret `Webhook__Token`. |

### Variables (não sensíveis)

| Variable | Usado em | Valor típico | Observação |
|---|---|---|---|
| `AWS_REGION` | `cd.yml`, `infra.yml` | `us-east-1` | Região dos recursos (Plano 09). |
| `EKS_CLUSTER_NAME` | `cd.yml` | output `cluster_name` (`oficina-eks`) | `aws eks update-kubeconfig`. |
| `ECR_REPOSITORY` | `cd.yml` | output `ecr_repository_url` (URL completa) | Base da tag `:$IMAGE_TAG`; alimenta o `envsubst`. |
| `JWT_ISSUER` | (referência) | `oficina-api` | **Hoje fixo no `k8s/configmap.yaml`**; listado para consistência/parametrização futura. |
| `JWT_AUDIENCE` | (referência) | `oficina-clients` | Idem — atualmente literal no ConfigMap; não injetado pelos workflows. |

> `JWT_ISSUER`/`JWT_AUDIENCE` **não** são consumidos pelos YAML atuais (o `k8s/configmap.yaml` do Plano 08 já traz `Jwt__Issuer`/`Jwt__Audience` como valores literais não sensíveis). Ficam na lista por completude e para uma futura parametrização do ConfigMap via `envsubst`. Se essa parametrização for feita, adicionar os dois ao passo "Aplicar Namespace e ConfigMap" do `cd.yml`.

---

## Auto-revisão (consistência) — corrigida inline

- **Permissions mínimas (least privilege):** `cd.yml` e `infra.yml` declaram no topo `permissions: { id-token: write, contents: read }` — o mínimo para OIDC + checkout, sem `write` em nada mais. O `codeql` do `ci.yml` mantém seu bloco próprio (`security-events: write`) apenas naquele job; os demais jobs do CI não precisam de token OIDC (não têm `permissions` extra). ✔
- **OIDC sem chave estática (corrigido/garantido):** ambos os workflows AWS usam `aws-actions/configure-aws-credentials@v4` com `role-to-assume` + `aws-region`, **sem** `aws-access-key-id`/`aws-secret-access-key`. O grep 5.5 falha o gate se achar qualquer credencial estática ou `AKIA…`. Alinha ao trust do Plano 09 (`repo:lcr-thiago-fernandes/fiap_15SOAT_fase1:*`). ✔
- **CD escopado a `main`:** `on.push.branches: [main]` (+ `workflow_dispatch`); o grep 5.3 falha se `develop`/`fase-2` aparecerem no `cd.yml`. Isso limita o uso do role OIDC de deploy a `main` (hardening). ✔
- **Ordem de apply correta (Plano 08):** namespace → configmap → **secret (real, via kubectl create)** → migration-job (envsubst) → **wait** → deployment (envsubst) → service → hpa → rollout status. O Namespace vem antes do Secret (o `kubectl create secret -n oficina` exige o namespace). Bate com `k8s/README.md`, exceto que o Secret é criado (não `kubectl apply -f secret.yaml`, que é só template). ✔
- **envsubst da imagem (corrigido — restrito):** usa `envsubst '${ECR_REPOSITORY} ${IMAGE_TAG}'` (lista explícita) em `migration-job.yaml` e `deployment.yaml` — os **únicos** dois manifestos com placeholders (confirmado por grep: só essas variáveis existem nos YAML). Restringir a lista evita que `envsubst` apague acidentalmente qualquer outro `$` futuro. `namespace/configmap/service/hpa` não têm placeholder → `kubectl apply -f` direto. ✔
- **Job imutável re-aplicado (corrigido):** um `Job` do K8s tem `spec.template` imutável; um segundo deploy com nova imagem faria `kubectl apply` falhar. Por isso o passo 7 faz `kubectl delete job oficina-migrate -n oficina --ignore-not-found` **antes** do `envsubst | apply`. ✔
- **Wait do Job antes do deploy:** `kubectl wait --for=condition=complete job/oficina-migrate --timeout=300s` roda **antes** de aplicar o `deployment.yaml`, garantindo schema migrado antes da API subir (coerente com `Bootstrap__ExecutarNoStartup=false` do ConfigMap). ✔
- **Nenhum segredo logado (corrigido):** segredos entram por bloco `env:` no step e são consumidos por `--from-literal`/`TF_VAR_*`; **não** há `echo`/`cat`/`set -x` sobre eles. A `ConnectionStrings__Default` é montada numa variável local `CONN` e passada direto ao `--from-literal`. O GitHub mascara valores de secrets no log; os greps 5.6 falham o gate se aparecer `echo ... secrets.` ou `set -x`. O `k8s/secret.yaml` (template com placeholders) **não** é aplicado. ✔
- **Consistência de nomes secrets/vars ↔ Plano 09:** `AWS_ROLE_ARN` ← `github_actions_role_arn`; `ECR_REPOSITORY` ← `ecr_repository_url`; `RDS_ENDPOINT` ← `rds_endpoint`; `EKS_CLUSTER_NAME` ← `cluster_name`; `DB_USER`/`DB_PASSWORD` ← `db_username`/`db_password`. `Database=oficina` na connection string bate com `var.db_name` default `oficina`. Porta `5432` fixa (RDS PostgreSQL). ✔
- **Actions pinadas por major:** `checkout@v4`, `setup-dotnet@v4`, `upload-artifact@v4`, `codeql-action/*@v3`, `configure-aws-credentials@v4`, `amazon-ecr-login@v2`, `setup-terraform@v3`. Coerente com o `dependabot.yml` (ecossistema `github-actions`) que proporá bumps. ✔
- **`dotnet format` advisory (não quebra o CI):** job `format` separado com `continue-on-error: true` no step — falha de estilo pré-existente **não** reprova o CI; comentário `TODO` indica como torná-lo bloqueante depois. Sem SonarCloud (evita conta/token). ✔
- **`nada de C# muda`:** só 3 YAML em `.github/workflows/`; `dotnet build Oficina.sln` no Step 5.13 segue 0 erros. ✔

## Riscos / ambiguidades a revisar

1. **`kubectl wait` em Job que FALHA:** `--for=condition=complete` só detecta sucesso; se a migração falhar, o `wait` fica bloqueado até `--timeout=300s` e então o step falha (deploy interrompido, correto). Não distingue "falhou" de "demorou". Melhoria opcional: rodar em paralelo um `kubectl wait --for=condition=failed` e abortar cedo, ou inspecionar `kubectl logs job/oficina-migrate` no `if: failure()`. Mantido simples por ora.
2. **Ferramentas pré-instaladas no runner:** `kubectl`, `envsubst` (gettext-base), AWS CLI v2 e Docker vêm no `ubuntu-latest`. Se uma futura imagem de runner remover `kubectl`, adicionar `azure/setup-kubectl@v4` (ou `aws eks ... | kubectl`) antes dos passos de deploy. Idem para `envsubst` (`sudo apt-get install -y gettext-base`).
3. **`workflow_dispatch` do CD só na branch default:** o botão "Run workflow" do `workflow_dispatch` só aparece para workflows presentes na branch **default** do repo. Enquanto a default for `main`, ok; se o CD só existir em `fase-2` (não mergeado), o dispatch manual não aparece até o merge. O push em `main` continua disparando normalmente após o merge.
4. **Trust OIDC amplo (`repo:...:*`) vs escopo a `main`:** o role do Plano 09 confia em qualquer ref do repo (`sub = repo:...:*`). O CD ser só em `main` reduz a superfície, mas para hardening real convém apertar o trust do Terraform para `sub = repo:...:ref:refs/heads/main` (deploy) e um role separado para infra. Fora do escopo deste plano (é mudança no `infra/`), mas anotar para o Plano 09/12.
5. **`infra.yml` e o chicken-and-egg:** o role `AWS_TERRAFORM_ROLE_ARN` precisa de permissões admin que o próprio Terraform cria — não existe antes do 1º apply. Documentado no cabeçalho do workflow e aqui: **provisionamento inicial é local** (`infra/README.md`). O `infra.yml` só serve para re-apply/destroy quando já houver um role de infra. A spec permite "manual OU local".
6. **`docker push :latest` e concorrência:** a tag `latest` é conveniência; o deploy usa **sempre** `:$IMAGE_TAG` (SHA), então `latest` nunca é ambíguo no rollout. O `concurrency` do CD (`cancel-in-progress: false`) serializa deploys da mesma ref para não misturar pushes/rollouts.
7. **`ECR_REPOSITORY` = URL completa (não só o nome):** o `cd.yml` assume que a Variable `ECR_REPOSITORY` é a **URL** do output `ecr_repository_url` (registry + repo). Se alguém preencher só `oficina-api`, o `docker push` falha por falta do host. Documentado na tabela de Variables; conferir no setup.
8. **`amazon-ecr-login@v2` × registry privado:** o login cobre o registry da conta assumida via OIDC. Como `ECR_REPOSITORY` já é a URL do mesmo registry, o `docker push` autentica. Se o repo ECR estiver em outra conta/registry, adicionar `registries:` ao input do login.
9. **Custo/limpeza:** cada deploy sobe um ELB (Service LoadBalancer, Plano 08) e mantém o cluster ligado. Sem relação direta com o CI/CD, mas lembrar do `terraform destroy` (via `infra.yml destroy` ou local) após a demo para não acumular custo (README do Plano 09).
