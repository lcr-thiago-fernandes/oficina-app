# ADR-018 — Ambientes por namespace em vez de clusters separados

**Status:** Aceita
**Data:** 2026-09-15

## Contexto

A Fase 3 precisa de dois ambientes, homologação e produção, num projeto acadêmico com custo
relevante: o control plane do Amazon EKS tem uma cobrança fixa por cluster — estimada em
US$ 73,00/mês na tabela de custo do `fase3-design-arquitetural.md` (seção 4, "Custo
estimado") — não de uma fatura real nem de um anexo (o ambiente não foi aplicado na
AWS). Dois clusters dobrariam esse valor e trariam, cada um, seu próprio node group e
seu próprio NAT Gateway.

## Decisão

Um único cluster `oficina-eks`, com dois namespaces: `oficina-hml` (homologação) e
`oficina-prd` (produção), conforme `k8s/namespace-hml.yaml` e `k8s/namespace-prd.yaml`. A
branch `develop` publica em `oficina-hml`, a branch `main` publica em `oficina-prd`
(`k8s/README.md`).

**V10** — existe um único HTTP API, e ele integra apenas com o listener de **produção** do
NLB (`aws_lb_listener.api["prd"]`, `oficina-infra-k8s/terraform/apigw.tf`). Homologação é
servida pelo listener `:81` do mesmo NLB interno (`aws_lb_listener.api["hml"]`,
`porta_listener = 81`), e esse listener só é alcançável **de dentro da VPC**: o SG do NLB
tem uma regra dedicada (`aws_vpc_security_group_ingress_rule.nlb_hml_da_vpc`) liberando a
porta 81 só para o CIDR da própria VPC, sem passar pelo API Gateway. Testar homologação
exige, portanto, estar dentro da VPC — um pod, port-forward ou bastion — não uma chamada
pública como em produção.

Os dois ambientes compartilham a **mesma instância** de RDS (`aws_db_instance.oficina`,
`oficina-infra-db/terraform/rds.tf`): não há um segundo banco por ambiente, e os dois
namespaces se conectam ao mesmo endpoint.

## Consequências

- ✅ Custo de um único control plane de EKS em vez de dois, e um único NAT Gateway
- ✅ Paridade de configuração entre ambientes: mesmo cluster, mesmos manifests
  parametrizados por `${NAMESPACE}`/`${AMBIENTE}`
- ⚠️ Isolamento é lógico, não físico: um consumo desmedido de recursos em `oficina-hml` (CPU,
  memória, conexões ao banco) afeta `oficina-prd`, e os dois ambientes compartilham o mesmo
  RDS
- ⚠️ Testar homologação exige estar dentro da VPC (bastion ou port-forward), o que torna a
  demonstração desse ambiente mais trabalhosa do que abrir uma URL pública
- ⚠️ Um `terraform destroy` no `oficina-infra-k8s` ou no `oficina-infra-db` derruba os dois
  ambientes juntos, sem possibilidade de preservar um e descartar o outro

Diagrama relacionado: [componentes](diagramas/componentes.md).
