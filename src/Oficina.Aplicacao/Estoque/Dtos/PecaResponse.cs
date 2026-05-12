namespace Oficina.Aplicacao.Estoque.Dtos;

public sealed record PecaResponse(
    Guid Id,
    string Sku,
    string Nome,
    decimal PrecoUnitario,
    int SaldoAtual,
    bool Ativo,
    DateTimeOffset CriadoEm);
