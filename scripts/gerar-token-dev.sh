#!/usr/bin/env bash
# Gera um JWT HS256 de desenvolvimento com as claims emitidas pela Lambda oficina-auth
# (a mesma claim set de tests/Oficina.Integracao.Testes/Auth/GeradorTokenDeTeste.cs).
# Vale para os tres servicos do docker-compose.fase4.yml (mesmo Jwt__Secret/Issuer/Audience).
# Uso: ./scripts/gerar-token-dev.sh [Admin|Atendente|Cliente] [documento]
#      (documento so para Cliente; le JWT_SECRET do .env se nao estiver definido)
set -euo pipefail

perfil="${1:-Admin}"
documento="${2:-}"
if [[ -z "${JWT_SECRET:-}" && -f "$(dirname "$0")/../.env" ]]; then
  JWT_SECRET="$(grep -E '^JWT_SECRET=' "$(dirname "$0")/../.env" | head -1 | cut -d= -f2- | tr -d '\r')"
fi
: "${JWT_SECRET:?defina JWT_SECRET (ou crie .env a partir de .env.example)}"
if [[ "$perfil" == "Cliente" && -z "$documento" ]]; then
  echo "perfil Cliente exige o documento (CPF) como segundo argumento" >&2
  exit 1
fi

b64url() { openssl base64 -A | tr '+/' '-_' | tr -d '='; }

exp=$(( $(date +%s) + 3600 ))
jti=$(openssl rand -hex 16)
h=$(openssl rand -hex 16)
sub="${h:0:8}-${h:8:4}-${h:12:4}-${h:16:4}-${h:20:12}"   # a Lambda emite sub como GUID
claimDocumento=""
[[ -n "$documento" ]] && claimDocumento=$(printf ',"documento":"%s"' "$documento")

header=$(printf '{"alg":"HS256","typ":"JWT"}' | b64url)
payload=$(printf '{"sub":"%s","perfil":"%s","nome":"Usuario Dev","jti":"%s"%s,"iss":"oficina-auth","aud":"oficina-api","exp":%s}' \
  "$sub" "$perfil" "$jti" "$claimDocumento" "$exp" | b64url)
assinatura=$(printf '%s.%s' "$header" "$payload" | openssl dgst -sha256 -hmac "$JWT_SECRET" -binary | b64url)

printf '%s.%s.%s\n' "$header" "$payload" "$assinatura"
