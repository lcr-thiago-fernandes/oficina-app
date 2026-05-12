namespace Oficina.Aplicacao.Estoque.Dtos;

public sealed record MovimentacaoResponse(
    Guid Id,
    string Tipo,
    int Quantidade,
    string Motivo,
    Guid? OrdemServicoId,
    DateTimeOffset CriadoEm);
