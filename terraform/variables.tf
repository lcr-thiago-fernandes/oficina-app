variable "region" {
  description = "Regiao AWS."
  type        = string
  default     = "us-east-1"
}

variable "project" {
  description = "Prefixo dos recursos e dos caminhos no SSM (mesmo dos outros repositorios)."
  type        = string
  default     = "oficina"
}

variable "namespaces_k8s" {
  description = "Namespaces cujas ServiceAccounts podem assumir a role IRSA."
  type        = list(string)
  default     = ["oficina-hml", "oficina-prd"]
}

variable "service_account" {
  description = "ServiceAccount do OS Service (k8s/serviceaccount.yaml)."
  type        = string
  default     = "oficina-api"
}
