namespace Oficina.Aplicacao.OrdensServico.Saga.Dtos;

/// <summary>Estado da saga de uma OS com a trilha de passos (GET /ordens-servico/{id}/saga).</summary>
public sealed record EstadoSagaResponse(
    Guid OrdemServicoId,
    long NumeroOs,
    string Estado,
    string? Motivo,
    string? PagamentoId,
    DateTimeOffset CriadaEm,
    DateTimeOffset AtualizadaEm,
    IReadOnlyList<PassoSagaResponse> Trilha);

public sealed record PassoSagaResponse(
    string Passo,
    string Resultado,
    bool Compensacao,
    DateTimeOffset OcorridoEm,
    string? Detalhe);
