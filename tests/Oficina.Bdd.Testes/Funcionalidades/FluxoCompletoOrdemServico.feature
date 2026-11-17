# language: pt
Funcionalidade: Fluxo completo da ordem de serviço orquestrado pela saga
  Para atender o cliente de ponta a ponta
  Como atendente da oficina
  Quero que abertura, orçamento, pagamento e execução sejam coordenados com compensação

  Contexto:
    Dado o serviço "Troca de óleo" de R$ 120,00 e a peça "Filtro de óleo" de R$ 35,50 com 10 unidades em estoque

  Cenário: Caminho feliz até Finalizada
    Quando o atendente abre uma OS com 1 "Troca de óleo" e 2 "Filtro de óleo"
    Então a peça "Filtro de óleo" fica com saldo 10 e 2 reservadas
    E o Billing recebe o comando GerarOrcamento com 2 itens
    Quando o Billing publica OrcamentoGerado
    Então a OS fica "AguardandoAprovacao"
    Quando o Billing publica OrcamentoAprovado
    Então a OS fica "AguardandoPagamento"
    Quando o Billing publica PagamentoConfirmado
    Então a peça "Filtro de óleo" fica com saldo 8 e 0 reservadas
    E a Execução recebe o comando IniciarExecucao
    Quando a Execução publica ExecucaoEnfileirada
    E a Execução publica StatusExecucaoAlterado "EmDiagnostico"
    E a Execução publica StatusExecucaoAlterado "EmReparo"
    E a Execução publica ExecucaoFinalizada
    Então a OS fica "Finalizada"
    E a saga fica "Concluida"
    E o Billing não recebe o comando EstornarPagamento
    E o Billing não recebe o comando CancelarOrcamento

  Cenário: Cliente rejeita o orçamento
    Quando o atendente abre uma OS com 1 "Troca de óleo" e 2 "Filtro de óleo"
    E o Billing publica OrcamentoGerado
    E o Billing publica OrcamentoRejeitado
    Então a OS fica "Cancelada"
    E a saga fica "Cancelada"
    E a peça "Filtro de óleo" fica com saldo 10 e 0 reservadas
    E o Billing não recebe o comando EstornarPagamento
    E o Billing não recebe o comando CancelarOrcamento

  Cenário: Pagamento recusado
    Quando o atendente abre uma OS com 1 "Troca de óleo" e 2 "Filtro de óleo"
    E o Billing publica OrcamentoGerado
    E o Billing publica OrcamentoAprovado
    E o Billing publica PagamentoRecusado
    Então a OS fica "Cancelada"
    E a saga fica "Cancelada"
    E a peça "Filtro de óleo" fica com saldo 10 e 0 reservadas
    E o Billing não recebe o comando EstornarPagamento
    E o Billing não recebe o comando CancelarOrcamento

  Cenário: Execução recusada dispara estorno e reposição de estoque
    Quando o atendente abre uma OS com 1 "Troca de óleo" e 2 "Filtro de óleo"
    E o Billing publica OrcamentoGerado
    E o Billing publica OrcamentoAprovado
    E o Billing publica PagamentoConfirmado
    E a Execução publica ExecucaoRecusada
    Então o Billing recebe o comando EstornarPagamento
    E a saga fica "Compensando"
    Quando o Billing publica PagamentoEstornado
    Então a OS fica "Cancelada"
    E a saga fica "Cancelada"
    E a peça "Filtro de óleo" fica com saldo 10 e 0 reservadas
    E o Billing não recebe o comando CancelarOrcamento
