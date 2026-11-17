# Sem credenciais no codigo: ambiente local (aws configure) ou OIDC no CI (infra.yml).
provider "aws" {
  region = var.region

  default_tags {
    tags = {
      Project   = var.project
      ManagedBy = "Terraform"
      Repo      = "oficina-app"
      Fase      = "fase-4"
    }
  }
}
