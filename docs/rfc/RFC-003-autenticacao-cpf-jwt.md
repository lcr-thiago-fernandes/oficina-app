# RFC-003 — Autenticação por CPF com JWT emitido por Lambda

**Status:** Aceita
**Data:** 2026-09-15
**Autor:** Thiago Fernandes
**ADRs relacionados:** ADR-003 (superseded), ADR-015, ADR-019

## 1. Contexto e problema

O enunciado da Fase 3 pede que o cliente se identifique pelo CPF e que as rotas
sensíveis da API — as ordens de serviço do próprio cliente, o autoatendimento — fiquem
protegidas, com a autenticação resolvida em arquitetura serverless. Na Fase 2
([ADR-003](../arquitetura/ADR-003-jwt-bootstrap-admin.md)) a própria `oficina-app` emitia
o JWT em `/auth/login` e a consulta do cliente era anônima: número da OS e documento iam
os dois em query string, sem token nenhum, e a política anti-enumeração se resumia a um
404 idêntico para "não existe" e "documento não confere".

Isso não atende ao pedido da Fase 3 em dois pontos. Primeiro, "serverless" deixa de ser
verdade se quem emite o token continua sendo o mesmo container ASP.NET Core que já roda
no EKS desde a Fase 2 — a emissão precisa sair da API. Segundo, uma consulta totalmente
anônima não distingue "CPF bem formado mas não cadastrado" de "CPF cadastrado mas de
outro dono" de nenhuma forma auditável: não há sujeito autenticado a quem atribuir a
consulta. A pergunta deste RFC é qual arquitetura de emissão e validação de token atende
ao "serverless" do enunciado sem introduzir mais infraestrutura do que o escopo da
oficina justifica.

## 2. Alternativas consideradas

| Critério | (a) Amazon Cognito user pool | (b) JWT Authorizer nativo do HTTP API, emissor OIDC próprio | (c) Lambda própria emitindo JWT + Lambda Authorizer — **escolhida** | (d) Manter a emissão na API (status quo da Fase 2) |
|---|---|---|---|---|
| Aderência ao "serverless" do enunciado | Serverless, mas resolve um problema que não é o do enunciado | Serverless, mas exige montar um emissor OIDC do zero | Serverless e o fluxo é exatamente "informe o CPF" | Não é serverless: quem emite continua sendo o pod da API |
| Infraestrutura nova introduzida | Um user pool inteiro, com fluxos de cadastro, confirmação de e-mail e recuperação de senha que o enunciado não pede | Um emissor OIDC publicando `/.well-known/jwks.json` — infraestrutura que não existe e este escopo não precisa | Duas funções Lambda pequenas (emissor + authorizer) e uma tabela DynamoDB para força bruta | Nenhuma; mas descumpre o requisito de arquitetura serverless |
| Controle sobre o fluxo de três erros (400/404/403) | Baixo: Cognito modela "usuário não existe" e "usuário desabilitado" com as próprias respostas de `InitiateAuth`, não com o vocabulário de CPF/status de cliente que o enunciado pede | Nenhum: o JWT Authorizer nativo só valida assinatura e expiração no Gateway — quem decide 400/404/403 continua sendo um emissor à parte, então essa alternativa não resolve sozinha o problema | Total: o caso de uso decide os três status explicitamente | Total, mas na API, não na Lambda |
| Custo | Cobrança por MAU (usuário ativo mensal) além do que já se paga por Lambda/API Gateway | Custo de manter um segundo serviço só para publicar chaves públicas | Sem custo adicional relevante: Lambda e DynamoDB dentro do free tier para o volume desta oficina | Zero custo novo, mas não resolve o requisito |

Cognito foi recusado porque o fluxo pedido pelo enunciado é "informe o CPF", não
"usuário e senha com verificação de e-mail": modelar CPF como identificador de login no
Cognito significa contornar o serviço — usar `PreSignUp`/`CustomAuth` para forçar um
fluxo que o user pool não foi desenhado para expressar — pagando a complexidade de um
serviço gerenciado sem usar nenhuma das garantias que o justificariam (federação,
verificação de e-mail, MFA gerenciado).

O JWT Authorizer nativo do HTTP API foi recusado porque ele só sabe validar tokens
assinados com chave assimétrica (RS256/ES256) emitidos por um emissor que publique
`/.well-known/jwks.json` — ou seja, adotar essa alternativa também exigiria montar (ou
adotar) um emissor OIDC, o mesmo problema de infraestrutura nova do Cognito, só que sem
nenhum dos benefícios de identidade gerenciada. Este ponto é aprofundado no
[ADR-015](../arquitetura/ADR-015-lambda-authorizer-hs256.md).

## 3. Decisão

Uma função `oficina-auth-api` (Lambda) é o **emissor único** de token; uma segunda
função, `oficina-auth-authorizer`, é quem o **HTTP API** consulta como Lambda Authorizer
antes de encaminhar qualquer requisição para `ANY /api/v1/{proxy+}`. A `oficina-app` **só
valida** o token que chega — ela nunca emite um — e o `AuthController` da Fase 2 foi
**removido** (confirmado: `ls src/Oficina.Api/Controllers/` não lista `AuthController.cs`
nesta branch).

### Contrato do token

Definido em `ContratoDoToken.cs` e usado por `EmissorDeToken.cs`, ambos no
`oficina-lambda-auth`:

| Claim/parâmetro | Valor |
|---|---|
| `iss` | `oficina-auth` |
| `aud` | `oficina-api` |
| `sub` | `Id` do sujeito (`Cliente.Id` ou `Usuario.Id`, conforme o perfil) — **informativo**, ver V6 abaixo |
| `perfil` | `Cliente`, `Atendente` ou `Admin` — comparado por igualdade exata (`RequireClaim`), case-sensitive |
| `documento` | CPF/CNPJ do cliente — presente **somente** quando `perfil = Cliente`; ausente e proibido nos demais perfis |
| `nome` | Nome do sujeito |
| `jti` | Identificador único do token, gerado a cada emissão |
| Algoritmo | HS256, segredo compartilhado (`oficina/jwt_secret`, mínimo de 32 caracteres) |
| Validade | 60 minutos |

A API valida `iss`, `aud`, assinatura e expiração ao aceitar o `Bearer` (defesa em
profundidade: mesmo que o API Gateway já tenha aceitado o token no Lambda Authorizer, a
API não confia em headers mapeados sem revalidar a assinatura por conta própria).

### O fluxo dos três erros

`POST /auth/cliente` responde exatamente três erros de negócio, na ordem em que são
verificados — validar o CPF, consultar a existência, consultar o status — mais um quarto
erro de limite de taxa:

| HTTP | Condição |
|---|---|
| `400 Bad Request` | CPF/CNPJ malformado ou com dígito verificador inválido (`Documento.Criar` lança antes de qualquer I/O) |
| `404 Not Found` | Nenhum cliente com aquele documento |
| `403 Forbidden` | Cliente existe mas está inativo |
| `429 Too Many Requests` | Limite de tentativas por origem estourado (força bruta, ver [ADR-015](../arquitetura/ADR-015-lambda-authorizer-hs256.md)) |

O fluxo completo, incluindo a interação com o DynamoDB de força bruta e o Secrets
Manager, está no diagrama de sequência:
[sequência de autenticação por CPF](../arquitetura/diagramas/sequencia-autenticacao-cpf.md).

### Mudanças na aplicação (`oficina-app`)

- Nova política de autorização `RequerCliente`: exige usuário autenticado, claim
  `perfil = Cliente` e presença da claim `documento`.
- `GET /api/v1/me/ordens-servico` e `GET /api/v1/me/veiculos` (novo `MeController`):
  substituem qualquer noção de autoatendimento anônimo — o documento do cliente vem
  **só** da claim do token, nunca de parâmetro de rota, query ou corpo.
- `GET /api/v1/consulta/{numeroOs}` deixa de ser anônima: passa a exigir
  `RequerCliente`, e o documento usado para checar propriedade vem da claim, não mais de
  query string.

### Autorização por propriedade: 404, não 403

Uma OS que não pertence ao cliente do token responde **404 Not Found**, não 403 — para
não revelar que o número da OS existe. Confirmado em
`ConsultarOrdemPorNumeroUseCase.ExecutarAsync` (`oficina-app/src/Oficina.Aplicacao/Consulta/ConsultarOrdemPorNumeroUseCase.cs`):
tanto "nenhuma OS com esse número" quanto "OS existe mas o documento do dono não bate com
o do token" retornam o mesmo `ResultadoConsulta.NaoEncontrada`/`DocumentoNaoConfere`, e o
`ConsultaController` (`oficina-app/src/Oficina.Api/Controllers/ConsultaController.cs`)
mapeia os dois para `NotFound()` — o mesmo corpo, sem distinção. O próprio comentário no
controller registra que a mitigação é só essa: existe uma diferença de tempo mensurável
entre os dois ramos (o caso "não encontrada" retorna após uma única consulta ao banco, o
caso "documento não confere" após duas), que essa resposta idêntica não esconde.

### V6 — propriedade por `documento`, não por `sub`

A especificação cita `sub` como identificador do sujeito; esta implementação autoriza por
`documento`. Três motivos, registrados em `oficina-app/README.md` (seção "Limitações
conhecidas e decisões registradas", item 1) e no código
(`oficina-app/src/Oficina.Api/Configuracao/ExtensoesClaims.cs`):

1. o token de Cliente é emitido **a partir do CPF** pela `oficina-auth-api` — é o dado que
   ela tem em mãos, e o único que identifica o mesmo sujeito nos dois lados sem um lookup
   adicional;
2. `sub` não tem significado uniforme entre perfis: num token de Cliente é o `Id` do
   `Cliente`, num token de Admin/Atendente o `Id` do `Usuario` — tabelas diferentes.
   Autorizar por ele exigiria saber o perfil antes de saber o que o identificador
   significa;
3. `documento` já é chave única e indexada em `cliente`, então autorizar por ele não
   encarece a consulta.

`sub` continua no token — é informativo, para rastreabilidade em log e APM. Consequência
direta: o leitor de `sub` que existia em `ExtensoesClaims` (`IdDoSujeito`) nunca tinha
chamador de produção — só os próprios testes — e foi **removido**, em vez de mantido como
API que aparenta ser suportada.

### V14 — `POST /auth/cliente` aceita CNPJ, e o campo se chama `cpf`

O corpo de `POST /auth/cliente` é `AutenticarClienteRequest(string? Cpf)`
(`oficina-lambda-auth/src/OficinaAuth.Aplicacao/Dtos.cs`), e o valor é validado por
`Documento.Criar` (`oficina-lambda-auth/src/OficinaAuth.Dominio/Documento.cs`), que aceita
tanto CPF (11 dígitos) quanto CNPJ (14 dígitos) com seus respectivos dígitos
verificadores. Isso é deliberado, não uma folga de validação: o cadastro de cliente desta
oficina permite cliente pessoa jurídica, então autenticar só por CPF deixaria o cliente PJ
sem caminho de login. O campo continua se chamando `cpf` — não `documento` — por
fidelidade literal ao enunciado da fase, que fala em "CPF".

## 4. Consequências

- ✅ Emissor único de token: a `oficina-app` deixa de ter qualquer superfície de emissão
  de JWT, o que elimina uma classe inteira de bug (dois emissores divergindo em TTL,
  claims ou segredo).
- ✅ Os três erros de negócio do enunciado (CPF malformado, CPF não cadastrado, cliente
  inativo) são endereços HTTP distintos e testáveis (400/404/403), em vez de um 401/403
  genérico que exigiria inspecionar o corpo da resposta para saber qual dos três
  aconteceu.
- ✅ A consulta de OS deixa de ser anônima por query string e passa a exigir token — o
  documento usado para checar propriedade não pode mais ser forjado por quem só sabe o
  número da OS de outra pessoa.
- ⚠️ O segredo HS256 compartilhado acopla os dois lados na rotação: trocar
  `oficina/jwt_secret` sem coordenar as duas Lambdas e o rollout da API em EKS invalida
  tokens válidos ou aceita tokens que deveriam ter expirado (detalhado no
  [ADR-015](../arquitetura/ADR-015-lambda-authorizer-hs256.md)).
- ⚠️ **Autenticar um cliente só com o CPF é autenticação fraca por construção, e isso não
  é um eufemismo.** O CPF é um identificador semipúblico — não é um segredo que só o
  titular conhece, é um dado que circula em notas fiscais, contratos, cadastros de
  terceiros. O limitador de força bruta por IP (10 falhas em 15 minutos) contém
  **enumeração** — impede varrer o espaço de CPFs válidos tentando descobrir quais estão
  cadastrados — mas não protege quem **já conhece** um CPF cadastrado: essa pessoa obtém
  um token válido em uma única tentativa, sem precisar de senha, sem segundo fator, sem
  qualquer verificação adicional de que é de fato o titular do documento. Isso é o desenho
  do próprio enunciado da fase, que pede identificação por CPF e não pede senha nem
  segundo fator para o cliente — não é um defeito desta implementação, que segue
  literalmente o que foi pedido. A mitigação natural para esse risco seria um segundo
  fator (SMS, e-mail, ou uma senha de fato), e está **fora de escopo** desta fase.

## 5. Como isto está implementado

| O quê | Onde |
|---|---|
| Caso de uso que decide os três erros de negócio (400 é validado antes, no domínio) | `oficina-lambda-auth/src/OficinaAuth.Aplicacao/AutenticarClienteUseCase.cs` |
| Contrato do token (claims, algoritmo, validade, tamanho mínimo do segredo) | `oficina-lambda-auth/src/OficinaAuth.Aplicacao/Tokens/ContratoDoToken.cs` |
| Emissão do token (claims efetivamente montadas, `sub`/`perfil`/`documento`/`nome`/`jti`) | `oficina-lambda-auth/src/OficinaAuth.Aplicacao/Tokens/EmissorDeToken.cs` |
| Mapeamento das exceções de negócio para 400/401/403/404/429 | `oficina-lambda-auth/src/OficinaAuth.Api/Configuracao/TratadorDeErros.cs` |
| Lambda Authorizer (resposta simples, sem policy IAM) | `oficina-lambda-auth/src/OficinaAuth.Authorizer/Function.cs` |
| Validação de CPF/CNPJ (aceita os dois — V14) | `oficina-lambda-auth/src/OficinaAuth.Dominio/Documento.cs` |
| Rotas `POST /auth/cliente`, `POST /auth/admin` e o authorizer pendurados no HTTP API | `oficina-lambda-auth/terraform/apigw.tf` |
| Autoatendimento do cliente autenticado (`/api/v1/me/*`) | `oficina-app/src/Oficina.Api/Controllers/MeController.cs` |
| Consulta de OS deixando de ser anônima, e o 404 de propriedade (não 403) | `oficina-app/src/Oficina.Api/Controllers/ConsultaController.cs`, `oficina-app/src/Oficina.Aplicacao/Consulta/ConsultarOrdemPorNumeroUseCase.cs` |
| Política `RequerCliente` (perfil + presença da claim `documento`) | `oficina-app/src/Oficina.Api/Configuracao/PoliticasDeAutorizacao.cs` |
| Leitura tipada da claim `documento`, e nota de por que não existe leitor de `sub` (V6) | `oficina-app/src/Oficina.Api/Configuracao/ExtensoesClaims.cs` |
| Configuração de validação do JWT na API (issuer, audience, chave simétrica) | `oficina-app/src/Oficina.Api/Configuracao/ConfiguracaoJwt.cs`, `oficina-app/src/Oficina.Infraestrutura/Auth/JwtOptions.cs` |
| Registro de V6 em prosa, fora do código | `oficina-app/README.md`, seção "Limitações conhecidas e decisões registradas", item 1 |
| Diagrama de sequência do fluxo completo, três erros + sucesso | `oficina-app/docs/arquitetura/diagramas/sequencia-autenticacao-cpf.md` |
