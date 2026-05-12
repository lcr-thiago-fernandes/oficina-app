namespace Oficina.Aplicacao.Consulta.Dtos;

public sealed record ItemConsultaResponse(
    string Tipo,            // "Servico" | "Peca"
    string Nome,
    decimal PrecoUnitario,
    int Quantidade,
    decimal Subtotal);
