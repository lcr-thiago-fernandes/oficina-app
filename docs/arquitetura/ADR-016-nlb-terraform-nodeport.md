# ADR-016 — NLB gerenciado pelo Terraform + Service NodePort

**Status:** Aceita
**Data:** 2026-09-15

## Contexto

O VPC Link do HTTP API (ver [ADR-014](ADR-014-api-gateway-http-api.md)) exige o ARN do
listener de um NLB no momento do `terraform apply`. Se o Kubernetes criasse o load balancer
(`Service type: LoadBalancer`), esse ARN só existiria depois do deploy da aplicação —
dependência circular entre `oficina-infra-k8s` (cria o API Gateway) e `oficina-app` (deploya
os pods): o primeiro repositório dependeria de um recurso que só o segundo cria.

## Decisão

O Terraform do `oficina-infra-k8s` é dono do NLB interno (`aws_lb.interno`, `internal =
true`), do target group (`aws_lb_target_group.api`, `target_type = "instance"`, health check
`GET /health` HTTP, matcher `200`, a cada 10 s), dos listeners (`aws_lb_listener.api`, um por
ambiente) e do `aws_autoscaling_attachment` que anexa o ASG do node group do EKS a cada
target group. O `oficina-app` só publica um `Service type: NodePort` com `nodePort` fixo por
ambiente (`k8s/service.yaml`). Assim o ARN do listener é conhecido já no `plan`, a ordem de
apply fica linear e o Service do lado do `oficina-app` se reduz a poucas linhas.

O NLB tem SG próprio (`sg-nlb`), com regras por referência de SG, não por CIDR:
`sg-vpclink` alcança `sg-nlb` nas portas de listener (80 produção, 81 homologação),
`sg-nlb` alcança `sg-nodes` (o SG dos nós do EKS) nos NodePorts (30080/30081), e o SG dos
nós recebe de volta essa mesma referência — a mesma porta cobre tráfego normal e health
check, porque o target group aponta `port = "traffic-port"`. `preserve_client_ip = "false"`
no target group faz com que o nó veja a ENI do NLB como origem, não o IP do cliente
original; a referência por SG continua inequívoca porque a regra de ingress no `sg-nodes`
não depende de qual IP chega, só de qual SG o alcançou.

Como parágrafo desta decisão, a alternativa descartada: AWS Load Balancer Controller com
`TargetGroupBinding` e targets `ip` apontando direto para os pods — um hop a menos (sem
passar pelo `kube-proxy` do nó) e health check direto no pod, não no nó. Foi recusada por
exigir IRSA, a instalação do controller via Helm chart e uma IAM policy adicional só para
esse controller gerenciar recursos de ELB — peças de infraestrutura que este escopo não
justifica com o NLB já resolvendo o problema de ARN. Fica como caminho de evolução se a
aplicação um dia precisar de health check por pod em vez de por nó.

## Consequências

- ✅ Ordem de apply linear: o ARN do listener existe no `plan` do `oficina-infra-k8s`, sem
  esperar o `oficina-app` publicar nada
- ✅ Service do `oficina-app` reduzido a poucas linhas, sem depender de um controller externo
- ⚠️ O `nodePort` vira contrato implícito entre dois repositórios (`oficina-infra-k8s` lê o
  valor via variável, `oficina-app` fixa o mesmo valor no manifest); se os dois divergirem,
  o target group aponta para uma porta que o Service não escuta e o erro só aparece no
  health check, não no `apply`
- ⚠️ Um hop a mais no caminho da requisição: NLB → nó (qualquer nó, `externalTrafficPolicy`
  padrão) → `kube-proxy` → pod, em vez de NLB → pod diretamente
- ⚠️ `preserve_client_ip = "false"` obriga a aplicação a ler `X-Forwarded-For` (adicionado
  pela integração do API Gateway) para conhecer a origem real do cliente, já que o NLB não
  repassa o IP original ao nó

Diagrama relacionado: [componentes](diagramas/componentes.md).
