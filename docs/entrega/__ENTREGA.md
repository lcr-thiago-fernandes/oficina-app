# Tech Challenge — Fase 1 — Documento de Entrega

**Curso:** Pós-Tech FIAP — Arquitetura de Software (15SOAT)
**Tema:** Sistema Integrado de Atendimento e Execução de Serviços para Oficina Mecânica
**Data de entrega:** _(preencher)_

---

## Equipe

**Nome do grupo:** _(preencher)_

| Nome | Discord |
|---|---|
| _(nome)_ | _(@usuario)_ |
| _(nome)_ | _(@usuario)_ |
| _(nome)_ | _(@usuario)_ |

---

## Links

| Item | Link |
|---|---|
| Repositório (privado, acesso a `soat-architecture`) | _(URL)_ |
| Vídeo de apresentação (YouTube unlisted, ≤ 15 min) | _(URL)_ |
| Documentação DDD (Miro — Event Storming) | _(URL)_ |
| Documentação técnica (README) | _(URL do README no GitHub)_ |

---

## Resumo da solução

MVP de back-end de oficina mecânica desenvolvido em **C# / .NET 8 / ASP.NET Core** sobre **PostgreSQL 16**, aplicando **Domain-Driven Design** (4 bounded contexts) e **Clean Architecture** (4 projetos com inversão de dependência).

### Funcionalidades entregues

- Gestão de clientes (PF/PJ) com veículos
- Catálogo de serviços com soft delete
- Controle de estoque com saldo (saldo nunca negativo, garantido por invariante + transação serializável)
- Ordem de Serviço com máquina de estados (Recebida → Em Diagnóstico → Aguardando Aprovação → Em Execução → Finalizada → Entregue, com Cancelada via rejeição do cliente)
- Snapshot de preço/nome nos itens da OS (preserva histórico)
- Aprovação/rejeição de orçamento pelo cliente via API pública (sem JWT)
- Baixa de estoque automática ao iniciar execução (transacional)
- Métricas administrativas (tempo médio de execução)
- Autenticação JWT com perfis Admin/Atendente, BCrypt, rate limiting
- Validação de CPF/CNPJ (dígitos verificadores), placa (formatos antigo + Mercosul)

### Cobertura de testes

- **Unitários do Domínio:** ≥ 90% (invariantes dos agregados, máquina de estados, VOs)
- **Unitários da Aplicação:** ≥ 80% (casos de uso com mocks)
- **Integração:** WebApplicationFactory + Testcontainers (Postgres real)
- **Total:** ≥ 80% (CI quebra se cair abaixo)

---

## Análise de vulnerabilidades

Resumo executivo do scan executado em _(data)_ usando **CodeQL**, **Dependabot** e `dotnet list package --vulnerable`:

| Severidade | Aberta | Mitigada |
|---|---|---|
| Crítica | _(N)_ | _(N)_ |
| Alta | _(N)_ | _(N)_ |
| Média | _(N)_ | _(N)_ |
| Baixa | _(N)_ | _(N)_ |

Detalhamento completo em [docs/relatorio-vulnerabilidades.md](../relatorio-vulnerabilidades.md).

---

## Como executar

```bash
git clone <url-do-repo>
cd oficina-mecanica
cp .env.example .env  # ajustar JWT_SECRET e ADMIN_BOOTSTRAP_PASSWORD
docker compose -f docker/docker-compose.yml --env-file .env up -d --build
# API:     http://localhost:8080
# Swagger: http://localhost:8080/swagger
```

Login inicial: `admin` / valor do `ADMIN_BOOTSTRAP_PASSWORD` (forçará troca no primeiro login).

---

## Anexos

- README detalhado: [README.md](../../README.md)
- Linguagem ubíqua: [docs/ddd/linguagem-ubiqua.md](../ddd/linguagem-ubiqua.md)
- Diagrama de contextos: [docs/ddd/contextos-delimitados.md](../ddd/contextos-delimitados.md)
- ADRs: [docs/arquitetura/](../arquitetura/)
- Spec do projeto: [docs/superpowers/specs/2026-05-03-tech-challenge-fase1-design.md](../superpowers/specs/2026-05-03-tech-challenge-fase1-design.md)
- Relatório de vulnerabilidades: [docs/relatorio-vulnerabilidades.md](../relatorio-vulnerabilidades.md)
