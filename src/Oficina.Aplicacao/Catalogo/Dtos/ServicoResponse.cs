namespace Oficina.Aplicacao.Catalogo.Dtos;

public sealed record ServicoResponse(
    Guid Id,
    string Nome,
    string Descricao,
    decimal PrecoBase,
    int TempoEstimadoMinutos,
    bool Ativo,
    DateTimeOffset CriadoEm);
