# Estado remoto no S3 com bloqueio via DynamoDB (Aula 2 — Providers e Backend).
#
# ATENCAO (chicken-and-egg): o bucket e a tabela abaixo precisam EXISTIR antes do
# primeiro `terraform init`. Crie-os UMA vez com a AWS CLI (ver infra/README.md -> Bootstrap).
#
# O nome do bucket S3 e GLOBALMENTE unico: ajuste o sufixo se ja estiver em uso
# (e replique o mesmo nome no comando de bootstrap do README).
terraform {
  backend "s3" {
    bucket         = "oficina-tfstate-fiap-15soat"
    key            = "fase2/terraform.tfstate"
    region         = "us-east-1"
    dynamodb_table = "oficina-tfstate-lock"
    encrypt        = true
  }
}
