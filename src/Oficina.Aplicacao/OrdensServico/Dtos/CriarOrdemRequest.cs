namespace Oficina.Aplicacao.OrdensServico.Dtos;

public sealed record CriarOrdemRequest(Guid ClienteId, Guid VeiculoId, string? Observacoes);
