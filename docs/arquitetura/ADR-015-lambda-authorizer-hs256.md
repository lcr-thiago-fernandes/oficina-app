# ADR-015 — Lambda Authorizer HS256 em vez do JWT Authorizer nativo

**Status:** Aceita
**Data:** 2026-09-15

## Contexto

O HTTP API do API Gateway tem um **JWT Authorizer nativo**, que exigiria menos código: o
Gateway valida o token sozinho, sem invocar uma função. Ele só sabe validar tokens assinados
com chave assimétrica (RS256/ES256) emitidos por um emissor OIDC que publique
`/.well-known/jwks.json` — o que significaria adotar Amazon Cognito ou montar um emissor
próprio só para expor esse endpoint de chaves públicas, em ambos os casos mais peça de
infraestrutura do que o escopo desta fase pede.

## Decisão

Usar um **Lambda Authorizer** do tipo `REQUEST`, payload format `2.0`, com
`enable_simple_responses = true`: a resposta é `{ isAuthorized, context }`, não uma policy IAM
— mais simples de gerar e de testar do que montar um documento de policy. O cache do resultado
é de **300 s** (`authorizer_result_ttl_in_seconds`), com chave implícita no header
`Authorization` (`identity_sources = ["$request.header.Authorization"]`).

O token é **HS256** com segredo compartilhado em `oficina/jwt_secret` (Secrets Manager, mínimo
32 caracteres): a Lambda `oficina-auth-api` assina, a Lambda `oficina-auth-authorizer` e a API
em EKS validam — as três leem o mesmo segredo. A consequência direta é que rotacionar o
segredo exige redeploy das duas funções Lambda **e** um novo rollout da API, coordenados; não
existe hand-off automático como o `kid`/JWKS de um emissor RS256 daria.

O authorizer roda **fora da VPC**, de propósito: ele só verifica assinatura e expiração do
JWT, sem tocar o banco, então não precisa de rota de rede para o RDS. Colocar uma Lambda em VPC
custa a criação de uma ENI e cold start maior, e o authorizer é invocado a cada requisição
protegida (mitigado pelo cache de 300 s, mas ainda assim no caminho crítico) — o custo não se
justifica para uma função sem I/O de rede privada.

A API em EKS **revalida** o JWT (assinatura, `iss`, `aud`, expiração) mesmo recebendo
`X-Perfil` / `X-Sub` / `X-Documento` já mapeados pelo API Gateway — defesa em profundidade: os
headers vêm de uma integração HTTP_PROXY que os aceitaria também se enviados diretamente por um
cliente malicioso, então a API não pode confiar neles sem conferir a assinatura por conta
própria.

A propriedade de uma Ordem de Serviço é resolvida pela claim **`documento`**, não por `sub`,
apesar de o enunciado citar `sub`. Três motivos, registrados também no `README.md` deste
repositório: o token de Cliente é emitido **a partir do CPF** pela `oficina-auth-api` — é o
dado que ela tem em mãos e o único que identifica o mesmo sujeito dos dois lados sem um lookup
adicional; `sub` não tem significado uniforme entre perfis — num token de Cliente seria o `Id`
do `Cliente`, num token de Admin/Atendente o `Id` do `Usuario`, tabelas diferentes, e autorizar
por ele exigiria saber o perfil antes de saber o que o identificador significa; `documento` já
é chave única e indexada em `cliente`, então autorizar por ele não encarece a consulta. `sub`
continua no token, só que informativo (rastreabilidade em log e APM). Consequência direta: o
leitor de `sub` que existia em `ExtensoesClaims` (`IdDoSujeito`) nunca tinha chamador de
produção — só os próprios testes — e foi **removido**, em vez de mantido como API que aparenta
ser suportada.

A proteção contra força bruta usa **DynamoDB** (`oficina-auth-tentativas`), não uma tabela do
Postgres da API — evita levar uma migration de schema só para isso ao `oficina-app`. O limite é
de **10 falhas por IP e 5 por identidade** (username) em uma janela de **15 minutos**, e ao
estourar qualquer um dos dois a Lambda responde `429`. O TTL do item no DynamoDB
(`expira_em`) é só faxina de armazenamento; quem decide se uma janela já expirou é o código
(`ProtecaoContraForcaBruta`), não o TTL do banco. No login de **cliente**, o balde é só por
origem — `AutenticarClienteUseCase` chama `ExigirPermitidoAsync(origem, identidade: null, ct)`
— porque o CPF é semipúblico e a ameaça real é enumeração por IP, não o comprometimento de uma
conta específica. No login de usuário/senha, `AutenticarAdminUseCase` passa os dois
(`ExigirPermitidoAsync(origem, username.Valor, ct)`): a origem contém enumeração distribuída e
a identidade protege uma conta específica atacada de várias origens.

## Consequências

- ✅ Sem Cognito nem emissor OIDC com JWKS: um único emissor de token (`oficina-auth-api`), sem
  peça de infraestrutura adicional
- ✅ Cache de 300 s no authorizer corta a maioria das invocações repetidas com o mesmo token
- ⚠️ HS256 com segredo compartilhado acopla emissor e validador na rotação: trocar o segredo
  sem coordenar os três (Lambda `auth-api`, Lambda `authorizer`, API em EKS) invalida tokens
  válidos ou aceita tokens que deveriam ter expirado
- ⚠️ O cache de 300 s do authorizer significa que revogar um token (ex.: desativar um usuário)
  não tem efeito imediato — o efeito só aparece depois que o cache daquele valor de
  `Authorization` expira
- ⚠️ Autenticar cliente só por CPF é o desenho do enunciado, não um defeito corrigível aqui:
  CPF é um identificador semipúblico, e quem já o conhece obtém um token válido; a mitigação
  natural seria um segundo fator, fora de escopo desta fase

Diagrama relacionado: [sequência de autenticação por CPF](diagramas/sequencia-autenticacao-cpf.md).
