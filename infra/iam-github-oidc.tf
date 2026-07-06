# Thumbprint do endpoint OIDC do GitHub, obtido dinamicamente (evita hardcode que expira).
data "tls_certificate" "github" {
  url = "https://token.actions.githubusercontent.com/.well-known/openid-configuration"
}

# OIDC provider: permite que o GitHub Actions troque um token OIDC por credenciais AWS
# temporarias (sem access_key/secret_key estaticos no repositorio).
resource "aws_iam_openid_connect_provider" "github" {
  url             = "https://token.actions.githubusercontent.com"
  client_id_list  = ["sts.amazonaws.com"]
  thumbprint_list = [data.tls_certificate.github.certificates[0].sha1_fingerprint]
  tags            = local.tags
}

# Trust policy RESTRITA: apenas o repo configurado, com aud = sts.amazonaws.com.
data "aws_iam_policy_document" "github_trust" {
  statement {
    effect  = "Allow"
    actions = ["sts:AssumeRoleWithWebIdentity"]

    principals {
      type        = "Federated"
      identifiers = [aws_iam_openid_connect_provider.github.arn]
    }

    condition {
      test     = "StringEquals"
      variable = "token.actions.githubusercontent.com:aud"
      values   = ["sts.amazonaws.com"]
    }

    condition {
      test     = "StringLike"
      variable = "token.actions.githubusercontent.com:sub"
      values   = ["repo:${var.github_repo}:*"]
    }
  }
}

resource "aws_iam_role" "github_actions" {
  name               = "${local.name}-github-actions"
  description        = "Role assumida pelo GitHub Actions via OIDC (push ECR + describe EKS)."
  assume_role_policy = data.aws_iam_policy_document.github_trust.json
  tags               = local.tags
}

# Policy minima 1: push/pull no ECR (o CI faz build e push da imagem).
data "aws_iam_policy_document" "github_ecr" {
  statement {
    sid       = "EcrAuth"
    effect    = "Allow"
    actions   = ["ecr:GetAuthorizationToken"]
    resources = ["*"] # GetAuthorizationToken exige recurso "*".
  }

  statement {
    sid    = "EcrPushPull"
    effect = "Allow"
    actions = [
      "ecr:BatchCheckLayerAvailability",
      "ecr:GetDownloadUrlForLayer",
      "ecr:BatchGetImage",
      "ecr:PutImage",
      "ecr:InitiateLayerUpload",
      "ecr:UploadLayerPart",
      "ecr:CompleteLayerUpload",
    ]
    resources = [aws_ecr_repository.oficina_api.arn]
  }
}

# Policy minima 2: describe do EKS (o acesso ao cluster em si vem da Access Entry do EKS).
# Resource "*" de proposito: referenciar module.eks.cluster_arn aqui criaria ciclo
# (module.eks -> role -> module.eks), pois o EKS ja depende desta role via access_entries.
data "aws_iam_policy_document" "github_eks" {
  statement {
    sid       = "EksDescribe"
    effect    = "Allow"
    actions   = ["eks:DescribeCluster", "eks:ListClusters"]
    resources = ["*"]
  }
}

resource "aws_iam_role_policy" "github_ecr" {
  name   = "${local.name}-github-ecr"
  role   = aws_iam_role.github_actions.id
  policy = data.aws_iam_policy_document.github_ecr.json
}

resource "aws_iam_role_policy" "github_eks" {
  name   = "${local.name}-github-eks"
  role   = aws_iam_role.github_actions.id
  policy = data.aws_iam_policy_document.github_eks.json
}
