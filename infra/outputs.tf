output "cluster_name" {
  description = "Nome do cluster EKS (usado em `aws eks update-kubeconfig`)."
  value       = module.eks.cluster_name
}

output "cluster_endpoint" {
  description = "Endpoint da API do cluster EKS."
  value       = module.eks.cluster_endpoint
}

output "ecr_repository_url" {
  description = "URL do repositorio ECR (alimenta ${var.ecr_repository_name}:TAG nos manifestos)."
  value       = aws_ecr_repository.oficina_api.repository_url
}

output "rds_endpoint" {
  description = "Host do RDS PostgreSQL (alimenta ConnectionStrings__Default no Secret do K8s)."
  value       = aws_db_instance.oficina.address
}

output "rds_port" {
  description = "Porta do RDS PostgreSQL."
  value       = aws_db_instance.oficina.port
}

output "github_actions_role_arn" {
  description = "ARN da role assumida pelo GitHub Actions via OIDC (secret AWS_ROLE_ARN no Plano 10)."
  value       = aws_iam_role.github_actions.arn
}

output "region" {
  description = "Regiao AWS dos recursos."
  value       = var.region
}

output "configure_kubectl" {
  description = "Comando para gerar o kubeconfig local apontando para o cluster."
  value       = "aws eks update-kubeconfig --region ${var.region} --name ${module.eks.cluster_name}"
}
