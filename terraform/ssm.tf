# Contrato publicado por este repositorio (RFC-004). Lido pelo cd.yml.
resource "aws_ssm_parameter" "irsa_role_arn" {
  name        = "/${var.project}/os-service/irsa_role_arn"
  description = "Role IRSA da ServiceAccount ${var.service_account} do OS Service (lida pelo cd.yml)."
  type        = "String"
  value       = aws_iam_role.os_service.arn
}
