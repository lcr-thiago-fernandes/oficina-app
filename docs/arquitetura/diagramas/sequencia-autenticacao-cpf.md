# Sequência — autenticação por CPF

`POST /auth/cliente`, com os quatro caminhos de erro e o de sucesso. Decisões em
[ADR-015](../ADR-015-lambda-authorizer-hs256.md) e
[RFC-003](../../rfc/RFC-003-autenticacao-cpf-jwt.md).

```mermaid
sequenceDiagram
    autonumber
    actor C as Cliente
    participant GW as API Gateway<br/>HTTP API
    participant L as Lambda<br/>oficina-auth-api
    participant D as DynamoDB<br/>oficina-auth-tentativas
    participant DB as RDS PostgreSQL
    participant S as Secrets Manager

    C->>GW: POST /auth/cliente { cpf }
    GW->>L: invoke
    Note over GW,L: throttling 10 rps / burst 20 em POST /auth/cliente<br/>SÓ SE throttling_auth_habilitado=true (default: false)

    L->>L: Documento.Criar(cpf)
    alt dígito verificador inválido
        L-->>C: 400 Bad Request
    else CPF bem formado
        L->>D: tentativas por origem (IP) na janela de 15 min
        alt 10 ou mais falhas da mesma origem
            D-->>L: acima do limite
            L-->>C: 429 Too Many Requests
        else dentro do limite
            L->>DB: SELECT id, nome, documento, ativo FROM clientes.cliente WHERE documento = @documento
            alt nenhuma linha
                DB-->>L: não encontrado
                L->>D: registra falha (origem)
                L-->>C: 404 Not Found
            else encontrado com ativo = false
                DB-->>L: cliente inativo
                L->>D: registra falha (origem)
                L-->>C: 403 Forbidden
            else encontrado e ativo
                DB-->>L: cliente ativo
                L->>S: GetSecretValue oficina/jwt_secret
                Note over L,S: segredo em cache estático por container
                S-->>L: texto puro, no mínimo 32 chars
                L->>L: assina HS256 — iss=oficina-auth, aud=oficina-api, exp=60 min
                L-->>C: 200 { access_token, expires_in, perfil: "Cliente" }
            end
        end
    end
```

## Por que esta ordem

| Passo | Motivo |
|---|---|
| Validar o CPF antes do limitador | um CPF malformado não consome tentativa nem I/O; o 400 é barato e não é sinal de ataque |
| Balde só por origem, sem identidade | o CPF é semipúblico: quem enumera troca de CPF, não de IP |
| Registrar falha no 404 **e** no 403 | os dois revelam se um CPF está cadastrado; sem contagem, a enumeração fica livre |
| Segredo em cache estático | a Lambda atende um request por container; reler o Secrets Manager a cada invocação dobraria a latência |

Os três caminhos de erro de negócio são os três verbos do enunciado: validar o CPF (400), consultar a
existência (404), consultar o status (403).

O throttling de `POST /auth/cliente` (10 rps, burst 20) só existe depois que a flag
`throttling_auth_habilitado` do `oficina-infra-k8s` é ligada — o `default` é `false`, porque a
rota nasce no `oficina-lambda-auth` e o `route_settings` do stage só aceita rota já existente
(mesma condicionalidade do [diagrama de componentes](componentes.md)).
