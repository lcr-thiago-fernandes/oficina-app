# metrics-server: necessario para o HPA (autoscaling/v2) do Plano 08 funcionar.
# Sem ele, as metricas de CPU/memoria ficam <unknown> e o HPA nao escala.
resource "helm_release" "metrics_server" {
  name       = "metrics-server"
  namespace  = "kube-system"
  repository = "https://kubernetes-sigs.github.io/metrics-server/"
  chart      = "metrics-server"
  version    = "3.12.2"

  # No EKS o kubelet usa certificado self-signed; flag necessaria para o metrics-server coletar.
  set {
    name  = "args[0]"
    value = "--kubelet-insecure-tls"
  }

  # So instala depois do cluster + node group prontos.
  depends_on = [module.eks]
}
