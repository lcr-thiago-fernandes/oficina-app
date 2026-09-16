namespace Oficina.Aplicacao.Autoatendimento.Dtos;

/// <summary>Visão enxuta da OS para o próprio cliente — sem dados internos da oficina.</summary>
public sealed record OrdemResumoResponse(
    long Numero,
    string Status,
    string Unidade,
    decimal TotalGeral,
    DateTimeOffset CriadaEm);
