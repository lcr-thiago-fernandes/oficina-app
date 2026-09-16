# ADR-019 — Duplicação consciente do Documento.cs na Lambda

**Status:** Aceita
**Data:** 2026-09-15

## Contexto

A Lambda de autenticação (`oficina-lambda-auth`) precisa validar CPF/CNPJ com exatamente
as mesmas regras que a API usa no cadastro de clientes — dígitos verificadores, formatos
aceitos, normalização — senão um documento aceito de um lado é recusado do outro, e o
cliente recebe 400 num fluxo que deveria funcionar (por exemplo, autenticar com o mesmo
CPF que o cadastro já validou). O value object `Documento`
(`oficina-app/src/Oficina.Dominio/Clientes/Documento.cs`, 85 linhas) não tem dependência
externa: nenhuma referência a EF Core, ASP.NET ou qualquer coisa que amarraria a Lambda ao
runtime da API.

## Decisão

Copiar o arquivo para o repositório da Lambda
(`oficina-lambda-auth/src/OficinaAuth.Dominio/Documento.cs`, também 85 linhas), com um job
de CI, `paridade-documento`
(`oficina-lambda-auth/.github/workflows/ci.yml`), rodando em todo PR e push. O job baixa a
cópia de referência diretamente de `oficina-app` na branch `develop`
(`raw.githubusercontent.com/lcr-thiago-fernandes/oficina-app/develop/src/Oficina.Dominio/Clientes/Documento.cs`)
e compara essa cópia contra o arquivo local com `diff`, descartando de **ambos** os lados
apenas a linha `namespace` (via `grep -v '^namespace '`) — a única diferença de fato
esperada entre as duas cópias, já que cada repositório usa seu próprio namespace raiz.
Qualquer outra divergência — mudança de regra de validação, formatação, comentário — falha
o job com a instrução de copiar o arquivo original e ajustar só o namespace.

Como parágrafo desta decisão, a alternativa descartada: publicar `Documento` como pacote
NuGet privado (GitHub Packages) consumido pelos dois repositórios. Isso exigiria
versionamento cruzado (a Lambda presa a uma versão do pacote até decidir atualizar), um
feed autenticado configurado nos dois CIs, e um release do pacote a cada mudança numa
classe que, historicamente, quase não muda — o value object de documento é estável desde
a Fase 1. O custo de manter esse pipeline de publicação supera o custo de uma cópia
vigiada por CI. Critério para reverter esta decisão: se a superfície compartilhada entre
os dois repositórios crescer além deste único value object — outras validações de domínio
precisando ser replicadas, por exemplo —, o pacote NuGet passa a valer o investimento.

## Consequências

- ✅ Os dois repositórios compilam e testam sozinhos, sem dependência de build cruzada nem
  feed de pacotes
- ✅ A divergência entre as duas cópias é detectada no PR pelo job `paridade-documento`, não
  em produção como um 400 inexplicável
- ⚠️ É duplicação de verdade: quem editar `Documento.cs` em `oficina-app` sem replicar a
  mudança em `oficina-lambda-auth` só vê o CI do outro repositório vermelho no próximo PR
  daquele repositório — não no momento da edição
- ⚠️ O job de paridade é uma dependência de fato entre repositórios: ele busca um arquivo de
  `oficina-app@develop` por URL fixa, e quebra se o caminho do arquivo mudar de qualquer
  lado (renomear a classe, mover de pasta, renomear o branch de referência)
