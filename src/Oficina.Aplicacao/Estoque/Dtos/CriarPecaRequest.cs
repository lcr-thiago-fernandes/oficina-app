namespace Oficina.Aplicacao.Estoque.Dtos;

public sealed record CriarPecaRequest(string Sku, string Nome, decimal PrecoUnitario);
