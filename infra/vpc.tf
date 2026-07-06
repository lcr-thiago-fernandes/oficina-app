# VPC via modulo oficial versionado (Aula 6 — Modulos). 2 AZs, subnets publicas + privadas.
module "vpc" {
  source  = "terraform-aws-modules/vpc/aws"
  version = "~> 5.13"

  name = "${local.name}-vpc"
  cidr = var.vpc_cidr
  azs  = local.azs

  # /24 por AZ: privadas para nos do EKS e RDS; publicas para NAT/LoadBalancer.
  private_subnets = ["10.0.1.0/24", "10.0.2.0/24"]
  public_subnets  = ["10.0.101.0/24", "10.0.102.0/24"]

  enable_nat_gateway   = true
  single_nat_gateway   = true # CUSTO: um unico NAT Gateway para toda a VPC.
  enable_dns_hostnames = true
  enable_dns_support   = true

  # Tags exigidas pelo EKS/AWS Load Balancer Controller para descoberta de subnets.
  public_subnet_tags = {
    "kubernetes.io/role/elb" = "1"
  }
  private_subnet_tags = {
    "kubernetes.io/role/internal-elb" = "1"
  }

  tags = local.tags
}
