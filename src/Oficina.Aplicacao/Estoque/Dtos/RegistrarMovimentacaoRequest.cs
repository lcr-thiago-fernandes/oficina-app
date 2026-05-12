namespace Oficina.Aplicacao.Estoque.Dtos;

public sealed record RegistrarMovimentacaoRequest(
    string Tipo,        // "Entrada" | "Saida"
    int Quantidade,
    string Motivo,
    Guid? OrdemServicoId);
