namespace Oficina.Aplicacao.Catalogo.Dtos;

public sealed record CriarServicoRequest(
    string Nome,
    string Descricao,
    decimal PrecoBase,
    int TempoEstimadoMinutos);
