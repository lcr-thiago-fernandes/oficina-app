output "irsa_role_arns" {
  description = "Role IRSA por ambiente (tambem em /oficina/os-service/<amb>/irsa_role_arn)."
  value       = { for amb, role in aws_iam_role.os_service : amb => role.arn }
}
