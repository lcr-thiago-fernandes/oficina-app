# IRSA: uma role para a ServiceAccount do OS Service nos namespaces hml e prd. O escopo
# por ambiente vem do prefixo dos nomes ({amb}-oficina-...): o MassTransit cria filas e
# topicos com esse prefixo, entao o padrao *-oficina-* cobre os dois ambientes.
# O LocalStack nao avalia IAM: a politica so e exercida de verdade na primeira subida em
# hml (conferir o log do pod: nenhum AccessDenied).

data "aws_iam_policy_document" "trust" {
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
      values   = [for ns in var.namespaces_k8s : "system:serviceaccount:${ns}:${var.service_account}"]
    }
  }
}

resource "aws_iam_role" "os_service" {
  name               = "${var.project}-os-service-irsa"
  description        = "IRSA do OS Service (ServiceAccount ${var.service_account}): SNS e SQS do MassTransit."
  assume_role_policy = data.aws_iam_policy_document.trust.json
}

data "aws_iam_policy_document" "mensageria" {
  # Filas de comando, _error/_skipped e a fila do bus por replica
  # ({amb}-oficina-os-bus-<pod>), criada e removida (DeleteQueue) pelo proprio pod.
  statement {
    sid    = "SqsFilas"
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
    resources = ["arn:aws:sqs:${var.region}:${local.conta}:*-${var.project}-*"]
  }

  statement {
    sid    = "SnsTopicos"
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
    resources = ["arn:aws:sns:${var.region}:${local.conta}:*-${var.project}-*"]
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
  name   = "${var.project}-os-service-mensageria"
  role   = aws_iam_role.os_service.id
  policy = data.aws_iam_policy_document.mensageria.json
}
