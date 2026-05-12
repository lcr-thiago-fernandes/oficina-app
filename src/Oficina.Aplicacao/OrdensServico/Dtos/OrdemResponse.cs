namespace Oficina.Aplicacao.OrdensServico.Dtos;

public sealed record OrdemResponse(
    Guid Id,
    long Numero,
    Guid ClienteId,
    Guid VeiculoId,
    string Status,
    string? Observacoes,
    decimal TotalServicos,
    decimal TotalPecas,
    decimal TotalGeral,
    DateTimeOffset CriadaEm,
    DateTimeOffset? DiagnosticadaEm,
    DateTimeOffset? EnviadaAprovacaoEm,
    DateTimeOffset? OrcamentoAprovadoEm,
    DateTimeOffset? OrcamentoRejeitadoEm,
    DateTimeOffset? IniciadaEm,
    DateTimeOffset? FinalizadaEm,
    DateTimeOffset? EntregueEm,
    IReadOnlyList<ItemServicoResponse> ItensServico,
    IReadOnlyList<ItemPecaResponse> ItensPeca);
