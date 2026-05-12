namespace Oficina.Aplicacao.OrdensServico.Dtos;

public sealed record MetricasTempoMedioResponse(
    int TotalOrdensConcluidas,
    double? TempoMedioMinutos,
    double? TempoMinimoMinutos,
    double? TempoMaximoMinutos);
