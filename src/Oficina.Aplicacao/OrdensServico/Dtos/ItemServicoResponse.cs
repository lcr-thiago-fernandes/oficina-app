namespace Oficina.Aplicacao.OrdensServico.Dtos;

public sealed record ItemServicoResponse(
    Guid Id, Guid ServicoId, string Nome, decimal PrecoUnitario, int Quantidade, decimal Subtotal);
