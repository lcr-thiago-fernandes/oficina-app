output "irsa_role_arn" {
  description = "Role IRSA do OS Service (tambem em /oficina/os-service/irsa_role_arn)."
  value       = aws_iam_role.os_service.arn
}
