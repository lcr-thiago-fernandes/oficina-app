namespace Oficina.Aplicacao.OrdensServico.Saga.Dtos;

/// <summary>Dados que a saga envia ao Billing no comando <c>GerarOrcamento</c>.</summary>
public sealed record PedidoOrcamento(
    long NumeroOs,
    string ClienteDocumento,
    string ClienteNome,
    string? ClienteEmail,
    IReadOnlyList<ItemPedidoOrcamento> Itens);

public sealed record ItemPedidoOrcamento(
    TipoItemPedido Tipo,
    Guid ReferenciaId,
    string Descricao,
    int Quantidade,
    decimal PrecoUnitario);

public enum TipoItemPedido
{
    Servico,
    Peca
}
