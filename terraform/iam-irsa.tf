# IRSA: uma role por ambiente, assumida SO pela ServiceAccount do namespace do ambiente.
# Escopo por prefixo de ambiente ({amb}-oficina-...): hml nunca toca fila/topico de prd e
# vice-versa. O LocalStack nao avalia IAM: a politica so e exercida de verdade na primeira
# subida em hml (conferir o log do pod: nenhum AccessDenied).

locals {
  ambientes = {
    prd = { namespace = "oficina-prd" }
    hml = { namespace = "oficina-hml" }
  }
}

data "aws_iam_policy_document" "trust" {
  for_each = local.ambientes

  statement {
    effect  = "Allow"
    actions = ["sts:AssumeRoleWithWebIdentity"]

    principals {
      type        = "Federated"
      identifiers = [local.oidc_provider_arn]
    }

    condition {
      test     = "StringEquals"
      variable = "${local.oidc_provider_url}:aud"
      values   = ["sts.amazonaws.com"]
    }

    condition {
      test     = "StringEquals"
      variable = "${local.oidc_provider_url}:sub"
      values   = ["system:serviceaccount:${each.value.namespace}:${var.service_account}"]
    }
  }
}

resource "aws_iam_role" "os_service" {
  for_each = local.ambientes

  name               = "${var.project}-os-service-irsa-${each.key}"
  description        = "IRSA do OS Service em ${each.value.namespace} (ServiceAccount ${var.service_account}): SNS e SQS do ambiente ${each.key}."
  assume_role_policy = data.aws_iam_policy_document.trust[each.key].json
}

data "aws_iam_policy_document" "mensageria" {
  for_each = local.ambientes

  # Filas de comando, _error/_skipped e a fila do bus por replica
  # ({amb}-oficina-os-bus-<pod>), criada e removida (DeleteQueue) pelo proprio pod.
  statement {
    sid    = "SqsFilasDoAmbiente"
    effect = "Allow"
    actions = [
      "sqs:CreateQueue",
      "sqs:DeleteQueue",
      "sqs:GetQueueUrl",
      "sqs:GetQueueAttributes",
      "sqs:SetQueueAttributes",
      "sqs:SendMessage",
      "sqs:ReceiveMessage",
      "sqs:DeleteMessage",
      "sqs:ChangeMessageVisibility",
      "sqs:PurgeQueue",
      "sqs:TagQueue",
      "sqs:ListQueueTags",
    ]
    resources = ["arn:aws:sqs:${var.region}:${local.conta}:${each.key}-${var.project}-*"]
  }

  statement {
    sid    = "SnsTopicosDoAmbiente"
    effect = "Allow"
    actions = [
      "sns:CreateTopic",
      "sns:Publish",
      "sns:Subscribe",
      "sns:Unsubscribe",
      "sns:GetTopicAttributes",
      "sns:SetTopicAttributes",
      "sns:ListSubscriptionsByTopic",
      "sns:GetSubscriptionAttributes",
      "sns:SetSubscriptionAttributes",
      "sns:TagResource",
    ]
    resources = ["arn:aws:sns:${var.region}:${local.conta}:${each.key}-${var.project}-*"]
  }

  # Listagens nao aceitam escopo por recurso.
  statement {
    sid       = "Listagens"
    effect    = "Allow"
    actions   = ["sns:ListTopics", "sqs:ListQueues"]
    resources = ["*"]
  }
}

resource "aws_iam_role_policy" "mensageria" {
  for_each = local.ambientes

  name   = "${var.project}-os-service-mensageria"
  role   = aws_iam_role.os_service[each.key].id
  policy = data.aws_iam_policy_document.mensageria[each.key].json
}
