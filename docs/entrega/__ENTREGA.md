# Tech Challenge — Fase 1 — Documento de Entrega

**Curso:** Pós-Tech FIAP — Arquitetura de Software (15SOAT)
**Tema:** Sistema Integrado de Atendimento e Execução de Serviços para Oficina Mecânica
**Data de entrega:** 12/05/2026

---

## Equipe

| Nome | Discord |
|---|---|
| Thiago Fernandes da Cruz | @thiago_64271 |


---

## Links

| Item | Link |
|---|---|
| Repositório (privado, acesso a `soat-architecture`) | https://github.com/lcr-thiago-fernandes/fiap_15SOAT_fase1 |
| Vídeo de apresentação | https://1drv.ms/v/c/fa2e7c7114d0ee1e/IQBnvt_g9AFhQKnVoKeJQzzuAUk0A4WCfI9GJiDfgnTHrPo?e=n6zU3K |
| Documentos | https://github.com/lcr-thiago-fernandes/fiap_15SOAT_fase1/tree/main/docs |
| README | https://github.com/lcr-thiago-fernandes/fiap_15SOAT_fase1/blob/main/README.md |


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

Detalhamento completo em [https://github.com/lcr-thiago-fernandes/fiap_15SOAT_fase1/blob/main/docs/entrega/4 - Relatório com análise de vulnerabilidades.md](https://github.com/lcr-thiago-fernandes/fiap_15SOAT_fase1/blob/main/docs/entrega/4 - Relatório com análise de vulnerabilidades.md).

---

## Como executar

```bash
git clone https://github.com/lcr-thiago-fernandes/fiap_15SOAT_fase1
cd fiap_15SOAT_fase1
cp .env.example .env  # ajustar ADMIN_BOOTSTRAP_PASSWORD
docker compose -f docker/docker-compose.yml --env-file .env up -d --build
# API:     http://localhost:8080
# Swagger: http://localhost:8080/swagger
```

Login inicial: `admin` / valor do `ADMIN_BOOTSTRAP_PASSWORD` (forçará troca no primeiro login).

