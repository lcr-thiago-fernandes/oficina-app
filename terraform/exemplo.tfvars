# Copie para local.tfvars (ignorado pelo git) para aplicar da propria maquina.
# Pre-requisito: /oficina/eks/oidc_provider_arn e /oficina/eks/oidc_provider_url ja
# publicados pelo oficina-infra-k8s.

region  = "us-east-1"
project = "oficina"

namespaces_k8s  = ["oficina-hml", "oficina-prd"]
service_account = "oficina-api"
