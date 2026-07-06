resource "aws_ecr_repository" "oficina_api" {
  name                 = var.ecr_repository_name
  image_tag_mutability = "MUTABLE"

  image_scanning_configuration {
    scan_on_push = true
  }

  tags = local.tags
}

# Lifecycle simples: mantem apenas as ultimas N imagens (custo/limpeza).
resource "aws_ecr_lifecycle_policy" "oficina_api" {
  repository = aws_ecr_repository.oficina_api.name

  policy = jsonencode({
    rules = [
      {
        rulePriority = 1
        description  = "Manter apenas as ultimas ${var.ecr_keep_last_images} imagens"
        selection = {
          tagStatus   = "any"
          countType   = "imageCountMoreThan"
          countNumber = var.ecr_keep_last_images
        }
        action = {
          type = "expire"
        }
      }
    ]
  })
}
