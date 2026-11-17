namespace Oficina.Aplicacao.OrdensServico.Saga.Dtos;

/// <summary>Dados que a saga envia à Execução no comando <c>IniciarExecucao</c>.</summary>
public sealed record PedidoExecucao(
    long NumeroOs,
    string Unidade,
    IReadOnlyList<ItemPedidoExecucao> Servicos,
    IReadOnlyList<ItemPedidoExecucao> Pecas);

public sealed record ItemPedidoExecucao(string Descricao, int Quantidade);
