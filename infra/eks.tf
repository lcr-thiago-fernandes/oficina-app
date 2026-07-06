# EKS via modulo oficial versionado (v20 — usa Access Entries nativos no lugar do aws-auth).
module "eks" {
  source  = "terraform-aws-modules/eks/aws"
  version = "~> 20.24"

  cluster_name    = local.cluster_name
  cluster_version = var.cluster_version

  # Endpoint publico para o CI/kubectl da pessoa (demo). Nos ficam em subnets privadas.
  cluster_endpoint_public_access = true

  vpc_id     = module.vpc.vpc_id
  subnet_ids = module.vpc.private_subnets

  # Addons gerenciados (rede e DNS do cluster).
  cluster_addons = {
    coredns    = {}
    kube-proxy = {}
    vpc-cni    = {}
  }

  # Um unico managed node group pequeno (custo).
  eks_managed_node_groups = {
    default = {
      instance_types = [var.node_instance_type]
      capacity_type  = "ON_DEMAND"

      min_size     = var.node_min_size
      max_size     = var.node_max_size
      desired_size = var.node_desired_size
    }
  }

  # Controle de acesso ao cluster via Access Entries (EKS API).
  authentication_mode                      = "API_AND_CONFIG_MAP"
  enable_cluster_creator_admin_permissions = true # quem faz o apply tambem administra (kubectl local).

  access_entries = {
    # Role de CI do GitHub Actions (Plano 10): admin do cluster para poder `kubectl apply`.
    github_ci = {
      principal_arn = aws_iam_role.github_actions.arn
      type          = "STANDARD"

      policy_associations = {
        admin = {
          policy_arn = "arn:aws:iam::aws:policy/AmazonEKSClusterAdminPolicy"
          access_scope = {
            type = "cluster"
          }
        }
      }
    }
  }

  tags = local.tags
}
