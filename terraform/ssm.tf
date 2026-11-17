# Contrato publicado por este repositorio (RFC-004). Lido pelo cd.yml (um por ambiente).
resource "aws_ssm_parameter" "irsa_role_arn" {
  for_each = local.ambientes

  name        = "/${var.project}/os-service/${each.key}/irsa_role_arn"
  description = "Role IRSA da ServiceAccount ${var.service_account} em ${each.value.namespace} (lida pelo cd.yml)."
  type        = "String"
  value       = aws_iam_role.os_service[each.key].arn
}
