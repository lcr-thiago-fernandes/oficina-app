# Relatório de Análise de Vulnerabilidades

**Repositório:** [https://github.com/lcr-thiago-fernandes/fiap_15SOAT_fase1](https://github.com/lcr-thiago-fernandes/fiap_15SOAT_fase1)
**Data do scan:** 2026-05-12
**Branch analisada:** `main`
**Responsável:** Thiago Fernandes da Cruz

## Ferramentas utilizadas

| Ferramenta | Tipo | Periodicidade |
|---|---|---|
| **GitHub CodeQL** | SAST (Static Application Security Testing) | * |
| **Dependabot** | SCA (Software Composition Analysis) | * |
| **`dotnet list package --vulnerable`** | SCA local | Em todo build local + CI |

## Configuração

- CodeQL: linguagem `csharp`, build mode `autobuild` — config em `.github/workflows/ci.yml`
- Dependabot: ecossistemas `nuget`, `github-actions`, `docker` — config em `.github/dependabot.yml`

---

## Resumo executivo

> **Status geral:** Nenhuma vulnerabilidade detectada nos projetos de produção (`Oficina.Dominio`, `Oficina.Aplicacao`, `Oficina.Infraestrutura`, `Oficina.Api`). Foram detectadas duas vulnerabilidades transitivas de severidade Alta nos projetos de **testes** (`System.Net.Http 4.3.0` e `System.Text.RegularExpressions 4.3.0`), trazidas indiretamente por dependências de bibliotecas de teste. Como esses pacotes não vão para produção (não fazem parte do binário publicado pela imagem Docker), o risco operacional é baixo. Mitigação planejada: atualizar as bibliotecas de teste que tragam essas transitivas (Moq, xUnit) ou forçar override no `Directory.Packages.props`.

| Severidade | Aberta | Mitigada | Aceita |
|---|---|---|---|
| Crítica | 0 | 0 | 0 |
| Alta    | 2 (somente em projetos de teste) | 0 | 2 (não vão para produção) |
| Média   | 0 | 0 | 0 |
| Baixa   | 0 | 0 | 0 |

---

## Achados detalhados

### CodeQL (SAST)

📎 **Screenshot:** `docs/relatorio-vulnerabilidades-codeql-summary.png` 

### Dependabot (SCA)

📎 **Screenshot:** `docs/relatorio-vulnerabilidades-dependabot-summary.png` 

### `dotnet list package --vulnerable`

Saída literal do comando rodado em 2026-05-03 (Windows, .NET 8 SDK):

```
  Determinando os projetos a serem restaurados...
  Todos os projetos estão atualizados para restauração.

As fontes a seguir foram usadas:
   https://api.nuget.org/v3/index.json
   C:\Program Files (x86)\Microsoft SDKs\NuGetPackages\

O projeto fornecido `Oficina.Dominio` não tem nenhum pacote vulnerável, considerando as fontes atuais.
O projeto fornecido `Oficina.Aplicacao` não tem nenhum pacote vulnerável, considerando as fontes atuais.
O projeto fornecido `Oficina.Infraestrutura` não tem nenhum pacote vulnerável, considerando as fontes atuais.
O projeto fornecido `Oficina.Api` não tem nenhum pacote vulnerável, considerando as fontes atuais.
O projeto `Oficina.Dominio.Testes` tem os pacotes vulneráveis a seguir
   [net8.0]: 
   Pacote Transitivo                     Resolvido   Severidade   URL do aviso                                     
   > System.Net.Http                     4.3.0       High         https://github.com/advisories/GHSA-7jgj-8wvc-jh57
   > System.Text.RegularExpressions      4.3.0       High         https://github.com/advisories/GHSA-cmhx-cq75-c4mj

O projeto `Oficina.Aplicacao.Testes` tem os pacotes vulneráveis a seguir
   [net8.0]: 
   Pacote Transitivo                     Resolvido   Severidade   URL do aviso                                     
   > System.Net.Http                     4.3.0       High         https://github.com/advisories/GHSA-7jgj-8wvc-jh57
   > System.Text.RegularExpressions      4.3.0       High         https://github.com/advisories/GHSA-cmhx-cq75-c4mj

O projeto `Oficina.Integracao.Testes` tem os pacotes vulneráveis a seguir
   [net8.0]: 
   Pacote Transitivo                     Resolvido   Severidade   URL do aviso                                     
   > System.Net.Http                     4.3.0       High         https://github.com/advisories/GHSA-7jgj-8wvc-jh57
   > System.Text.RegularExpressions      4.3.0       High         https://github.com/advisories/GHSA-cmhx-cq75-c4mj
```

#### Análise dos achados transitivos

| Pacote | Versão | Severidade | Advisory | Trazido por | Aplica a produção? |
|---|---|---|---|---|---|
| `System.Net.Http` | 4.3.0 | High | [GHSA-7jgj-8wvc-jh57](https://github.com/advisories/GHSA-7jgj-8wvc-jh57) | Bibliotecas de teste (Moq/xUnit) | **Não** — apenas em `tests/` |
| `System.Text.RegularExpressions` | 4.3.0 | High | [GHSA-cmhx-cq75-c4mj](https://github.com/advisories/GHSA-cmhx-cq75-c4mj) | Bibliotecas de teste | **Não** — apenas em `tests/` |

**Mitigação planejada (curto prazo):** quando a equipe atualizar as bibliotecas de teste (Moq, xUnit, Coverlet) para versões mais recentes ou adicionar uma referência direta às versões corrigidas (`>= 4.3.4`) os avisos desaparecerão. Como esses pacotes nunca são publicados na imagem Docker (multi-stage build copia apenas o output de `Oficina.Api`), o risco operacional é considerado **baixo**.

---

## Mitigações aplicadas

1. **BCrypt cost 12** para senhas (resistente a brute-force GPU em hardware atual)
2. **JWT com chave ≥ 32 caracteres** — falha de inicialização se menor
3. **Rate limiting** no login (5 tentativas / 15 min por IP)
4. **Headers seguros** (X-Content-Type-Options, X-Frame-Options, HSTS em prod)
5. **Swagger só em Development**
6. **Anti-enumeração** na consulta pública (404 idêntico para "não existe" e "documento errado")
7. **Filtro de logs sensíveis** (Serilog não loga senha, JWT, ou documento completo)
8. **Queries parametrizadas** via EF Core (sem string concat com SQL)

---

## OWASP Top 10 — mapeamento

| Risco | Mitigação |
|---|---|
| A01 Broken Access Control | `[Authorize]` + perfil; consulta pública só com documento conferente |
| A02 Cryptographic Failures | BCrypt + HTTPS em prod + segredos via env |
| A03 Injection | EF Core (queries parametrizadas) |
| A04 Insecure Design | Invariantes nos agregados |
| A05 Security Misconfiguration | Headers seguros; Swagger só em dev; user `app` no container |
| A06 Vulnerable Components | Dependabot + dotnet list --vulnerable |
| A07 Auth Failures | Rate limiting + BCrypt + JWT com expiração |
| A08 Software/Data Integrity | CodeQL + Dependabot |
| A09 Logging Failures | Serilog estruturado; nunca logar credenciais |
| A10 SSRF | Sem chamadas HTTP saintes no MVP |

---

## Próximos passos (fora do MVP)

- Migrar JWT para RS256 (chave assimétrica)
- Adicionar refresh tokens
- Implementar HTTPS obrigatório em produção (HSTS, redirect 301)
- Auditoria de mudanças de status de OS (quem aprovou, quando)
- Testes de penetração antes do deploy em produção
- Atualizar bibliotecas de teste para eliminar transitivas vulneráveis (`System.Net.Http 4.3.0`, `System.Text.RegularExpressions 4.3.0`)
