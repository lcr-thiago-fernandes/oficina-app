# DB subnet group nas subnets privadas (RDS nunca em subnet publica).
resource "aws_db_subnet_group" "oficina" {
  name       = "${local.name}-db-subnet-group"
  subnet_ids = module.vpc.private_subnets
  tags       = local.tags
}

# Security Group do RDS: sem ingress default; libera 5432 SOMENTE do SG dos nos do EKS.
resource "aws_security_group" "rds" {
  name        = "${local.name}-rds-sg"
  description = "Acesso ao PostgreSQL a partir dos nos do EKS"
  vpc_id      = module.vpc.vpc_id
  tags        = local.tags
}

resource "aws_security_group_rule" "rds_ingress_eks" {
  type                     = "ingress"
  description              = "PostgreSQL 5432 a partir dos nos do EKS"
  from_port                = 5432
  to_port                  = 5432
  protocol                 = "tcp"
  security_group_id        = aws_security_group.rds.id
  source_security_group_id = module.eks.node_security_group_id
}

resource "aws_security_group_rule" "rds_egress_all" {
  type              = "egress"
  description       = "Saida liberada"
  from_port         = 0
  to_port           = 0
  protocol          = "-1"
  cidr_blocks       = ["0.0.0.0/0"]
  security_group_id = aws_security_group.rds.id
}

resource "aws_db_instance" "oficina" {
  identifier     = "${local.name}-postgres"
  engine         = "postgres"
  engine_version = var.db_engine_version
  instance_class = var.db_instance_class

  allocated_storage = var.db_allocated_storage
  storage_type      = "gp3"
  storage_encrypted = true

  db_name  = var.db_name
  username = var.db_username
  password = var.db_password # SENSIVEL — vem de var.db_password (nunca hardcoded).

  db_subnet_group_name   = aws_db_subnet_group.oficina.name
  vpc_security_group_ids = [aws_security_group.rds.id]

  multi_az            = false # CUSTO: single-AZ.
  publicly_accessible = false # NUNCA exposto na internet.

  skip_final_snapshot = true  # demo: destroy sem snapshot final.
  deletion_protection = false # demo: permite `terraform destroy`.
  apply_immediately   = true

  tags = local.tags
}
