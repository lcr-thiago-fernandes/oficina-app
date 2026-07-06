# AZs disponiveis na regiao — usamos as 2 primeiras (custo/simplicidade).
data "aws_availability_zones" "disponiveis" {
  state = "available"
}

locals {
  name         = var.project
  cluster_name = "${var.project}-eks"
  azs          = slice(data.aws_availability_zones.disponiveis.names, 0, 2)

  tags = {
    Project   = var.project
    ManagedBy = "Terraform"
    Fase      = "fase-2"
  }
}
