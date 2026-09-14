#!/usr/bin/env bash
# Aplica protecao de branch em main e develop.
#
# Requisito da Fase 3: "Branch main/master protegida (sem commits diretos)" e
# "Uso obrigatorio de Pull Requests para merge". Fica versionado para a
# configuracao ser auditavel e reproduzivel nos quatro repositorios, em vez de
# depender de cliques na interface.
#
# Uso: ./scripts/setup-branch-protection.sh <owner>/<repo>
set -euo pipefail

REPO="${1:?informe owner/repo}"

for BRANCH in main develop; do
  echo "Protegendo ${REPO}@${BRANCH}..."

  # required_status_checks fica com contexts vazio ate o CI rodar a primeira vez:
  # exigir um check que nunca executou trava qualquer merge.
  gh api -X PUT "repos/${REPO}/branches/${BRANCH}/protection" \
    -H "Accept: application/vnd.github+json" \
    --input - <<'JSON'
{
  "required_status_checks": {
    "strict": true,
    "contexts": []
  },
  "enforce_admins": false,
  "required_pull_request_reviews": {
    "dismiss_stale_reviews": true,
    "require_code_owner_reviews": false,
    "required_approving_review_count": 1
  },
  "restrictions": null,
  "allow_force_pushes": false,
  "allow_deletions": false
}
JSON
done

echo
echo "OK. Depois do primeiro CI verde, adicione o check obrigatorio:"
echo "  gh api -X PATCH repos/${REPO}/branches/main/protection/required_status_checks \\"
echo "    -f 'contexts[]=build-test'"
