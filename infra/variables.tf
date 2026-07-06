variable "region" {
  description = "Regiao AWS onde os recursos serao criados."
  type        = string
  default     = "us-east-1"
}

variable "project" {
  description = "Prefixo/nome do projeto usado para nomear e taguear recursos."
  type        = string
  default     = "oficina"
}

variable "cluster_version" {
  description = "Versao do Kubernetes no EKS."
  type        = string
  default     = "1.30"
}

variable "vpc_cidr" {
  description = "CIDR da VPC."
  type        = string
  default     = "10.0.0.0/16"
}

# --- Node group ---
variable "node_instance_type" {
  description = "Tipo de instancia dos nos do EKS (t3.small e mais barato; t3.medium e mais folgado)."
  type        = string
  default     = "t3.medium"
}

variable "node_desired_size" {
  description = "Quantidade desejada de nos."
  type        = number
  default     = 2
}

variable "node_min_size" {
  description = "Quantidade minima de nos."
  type        = number
  default     = 1
}

variable "node_max_size" {
  description = "Quantidade maxima de nos."
  type        = number
  default     = 3
}

# --- RDS PostgreSQL ---
variable "db_name" {
  description = "Nome do banco inicial criado no RDS."
  type        = string
  default     = "oficina"
}

variable "db_username" {
  description = "Usuario administrador do RDS PostgreSQL."
  type        = string
  default     = "oficina_admin"
}

variable "db_password" {
  description = "Senha do usuario do RDS. SENSIVEL — informe via TF_VAR_db_password ou terraform.tfvars (nao commitado)."
  type        = string
  sensitive   = true
  # SEM default de proposito: forca o operador a fornecer a senha; nunca committada.
}

variable "db_instance_class" {
  description = "Classe de instancia do RDS."
  type        = string
  default     = "db.t3.micro"
}

variable "db_allocated_storage" {
  description = "Armazenamento do RDS em GB (gp3)."
  type        = number
  default     = 20
}

variable "db_engine_version" {
  description = "Versao major do PostgreSQL no RDS."
  type        = string
  default     = "16"
}

# --- ECR / GitHub OIDC ---
variable "ecr_repository_name" {
  description = "Nome do repositorio ECR da imagem da API."
  type        = string
  default     = "oficina-api"
}

variable "ecr_keep_last_images" {
  description = "Quantas imagens manter no ECR (lifecycle policy)."
  type        = number
  default     = 10
}

variable "github_repo" {
  description = "Repositorio GitHub (owner/nome) autorizado a assumir a role via OIDC."
  type        = string
  default     = "lcr-thiago-fernandes/fiap_15SOAT_fase1"
}
