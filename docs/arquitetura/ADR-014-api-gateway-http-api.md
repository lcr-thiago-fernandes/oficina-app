# ADR-014 — API Gateway HTTP API + VPC Link

**Status:** Aceita
**Data:** 2026-09-15

## Contexto

A Fase 3 exige um API Gateway à frente da aplicação, único ponto de entrada público para
`/auth/*` (Lambdas) e `/api/v1/*` (a API em EKS). A escolha era entre **REST API** e
**HTTP API** do API Gateway v2. O cluster EKS fica em subnets privadas, sem NLB público, então
o Gateway só alcança os pods através de um **VPC Link**, qualquer que seja o tipo escolhido.

## Decisão

Usar **HTTP API**, não REST API: custo por milhão de requisições menor, latência menor (menos
hops internos na AWS) e a maior parte dos recursos exclusivos de REST API não se aplica a este
escopo — WAF nativo (não há orçamento para WAF nesta fase), chaves de API (não há plano de
cobrança por cliente) e modelos de request/response (a validação de payload já é feita pela
aplicação, em FluentValidation). Perde-se transformação de payload mais rica e uma integração
Lambda mais configurável, mas nenhum dos dois é usado aqui.

Um **único** HTTP API (`aws_apigatewayv2_api`), stage único `$default` com `auto_deploy = true`:
não há stages `dev`/`prd` separados porque homologação e produção já são segregadas por
namespace no cluster, não por Gateway (ver [ADR-018](ADR-018-ambientes-por-namespace.md)).

Rotas e quem as cria:

| Rota | Cria |
|---|---|
| `GET /health` | `oficina-infra-k8s` |
| `GET /swagger` | `oficina-infra-k8s` |
| `GET /swagger/{proxy+}` | `oficina-infra-k8s` |
| `POST /api/v1/ordens-servico/{id}/orcamento/aprovacao` | `oficina-infra-k8s` |
| `POST /auth/cliente` | `oficina-lambda-auth` |
| `POST /auth/admin` | `oficina-lambda-auth` |
| `ANY /api/v1/{proxy+}` | `oficina-lambda-auth` |

A rota protegida `ANY /api/v1/{proxy+}` nasce no **`oficina-lambda-auth`**, não no
`oficina-infra-k8s` que cria o próprio API — porque o authorizer só existe depois de o
`oficina-lambda-auth` aplicar, e ele precisa apontar o `authorizer_id` na própria criação da
rota. Definir a rota no `oficina-infra-k8s` e o authorizer no `oficina-lambda-auth` obrigaria
uma segunda passada só para anexar o authorizer; e duas `route_key` iguais definidas em dois
Terraform state diferentes dão `ConflictException` no apply, então a rota só pode existir em
um dos dois repositórios. As rotas mais específicas do `oficina-infra-k8s` (`GET /health`,
`GET /swagger/{proxy+}`, `POST .../orcamento/aprovacao`) vencem `ANY /api/v1/{proxy+}` sem
precisar de authorizer — comportamento nativo de resolução de rotas do HTTP API.

Há **duas** integrações HTTP_PROXY via VPC Link para o mesmo NLB, não uma: `vpc_link`, usada
pela rota protegida, mapeia `append:header.X-Perfil`, `append:header.X-Sub` e
`append:header.X-Documento` a partir de `$context.authorizer.perfil` /
`$context.authorizer.sub` / `$context.authorizer.documento`; `vpc_link_publica`, usada pelas
rotas sem authorizer, não mapeia nada. A separação existe porque nas rotas públicas
`$context.authorizer.*` não existe — um mapeamento de header apontando para um contexto
inexistente arriscaria virar erro 500 em `GET /health`, justamente o endpoint que um alerta de
uptime consulta.

O throttling de `POST /auth/*` (10 rps, burst 20) **não é uma propriedade incondicional do
Gateway**. `aws_apigatewayv2_stage.default`, no `oficina-infra-k8s`, só aceita `route_settings`
para rotas que já existem no state do próprio Gateway; como `/auth/cliente` e `/auth/admin`
nascem no `oficina-lambda-auth`, que aplica depois, o `route_settings` fica atrás da flag
`throttling_auth_habilitado` (`bool`, default **`false`**). Ligar o throttling é uma decisão de
processo em três etapas, não um detalhe: (1) apply do `oficina-infra-k8s` com a flag em
`false`, criando o Gateway e o teto global de 200 rps / burst 500; (2) apply do
`oficina-lambda-auth`, que cria as rotas `/auth/*`; (3) `gh variable set
THROTTLING_AUTH_HABILITADO --body true` seguido de um novo apply do `oficina-infra-k8s` em
`main`, que então aplica o `route_settings` de 10 rps / burst 20 sobre as rotas já existentes.
Enquanto a etapa 3 não roda, `/auth/*` responde só ao teto global do stage.

## Consequências

- ✅ Custo por milhão de requisições e latência menores que REST API, sem uso de nenhum recurso
  exclusivo dela
- ✅ Superfície de rede reduzida a um único ponto de entrada público, com todo tráfego para o
  cluster passando por VPC Link
- ✅ Rotas públicas isoladas da integração que mapeia `$context.authorizer.*`, sem risco de
  mapeamento quebrado em `GET /health`
- ⚠️ A terceira etapa do throttling de `/auth/*` é manual; se ninguém a executar após o apply
  do `oficina-lambda-auth`, essas rotas seguem protegidas só pelo teto global de 200 rps / burst
  500 do stage, não pelos 10 rps / burst 20 específicos
- ⚠️ Desmontar o ambiente exige a ordem inversa: voltar `throttling_auth_habilitado` para
  `false` e reaplicar o `oficina-infra-k8s` antes de destruir o `oficina-lambda-auth`, senão o
  `route_settings` referencia uma rota que já não existe

Diagrama relacionado: [componentes](diagramas/componentes.md).
