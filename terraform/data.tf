# Contrato entre repositorios (RFC-004): identificadores no SSM, nunca terraform_remote_state.
# Publicados pelo oficina-infra-k8s. Se faltar um parametro, o plan falha com o nome dele.
# insecure_value: sao identificadores, nao segredos.

data "aws_caller_identity" "atual" {}

data "aws_ssm_parameter" "eks_oidc_provider_arn" {
  name = "/${var.project}/eks/oidc_provider_arn"
}

data "aws_ssm_parameter" "eks_oidc_provider_url" {
  name = "/${var.project}/eks/oidc_provider_url"
}

locals {
  conta             = data.aws_caller_identity.atual.account_id
  oidc_provider_arn = data.aws_ssm_parameter.eks_oidc_provider_arn.insecure_value
  oidc_provider_url = trimprefix(data.aws_ssm_parameter.eks_oidc_provider_url.insecure_value, "https://") # defesa: o contrato ja e sem https://
}
