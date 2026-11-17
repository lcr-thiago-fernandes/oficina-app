# Versoes alinhadas ao oficina-infra-k8s (aws 5.x de proposito: os outros repositorios
# estao em aws 5; a major 6 muda sintaxe).
terraform {
  required_version = ">= 1.10" # use_lockfile no backend S3

  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 5.60"
    }
  }
}
