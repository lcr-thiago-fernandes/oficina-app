# Provider AWS: regiao via variavel (default us-east-1). Sem credenciais no codigo —
# autenticacao vem do ambiente (aws configure / variaveis de ambiente / OIDC no CI).
provider "aws" {
  region = var.region

  default_tags {
    tags = local.tags
  }
}

# Autenticacao no cluster para os providers kubernetes/helm: endpoint + CA + token
# derivados dos outputs do EKS (via data sources). O `name` depende de module.eks,
# entao a leitura destes data sources so acontece em tempo de APPLY (apos o cluster existir).
data "aws_eks_cluster" "este" {
  name = module.eks.cluster_name
}

data "aws_eks_cluster_auth" "este" {
  name = module.eks.cluster_name
}

provider "kubernetes" {
  host                   = data.aws_eks_cluster.este.endpoint
  cluster_ca_certificate = base64decode(data.aws_eks_cluster.este.certificate_authority[0].data)
  token                  = data.aws_eks_cluster_auth.este.token
}

provider "helm" {
  kubernetes {
    host                   = data.aws_eks_cluster.este.endpoint
    cluster_ca_certificate = base64decode(data.aws_eks_cluster.este.certificate_authority[0].data)
    token                  = data.aws_eks_cluster_auth.este.token
  }
}
