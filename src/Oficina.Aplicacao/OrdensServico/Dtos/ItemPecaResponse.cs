namespace Oficina.Aplicacao.OrdensServico.Dtos;

public sealed record ItemPecaResponse(
    Guid Id, Guid PecaId, string Nome, decimal PrecoUnitario, int Quantidade, decimal Subtotal);
