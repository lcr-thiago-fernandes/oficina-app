namespace Oficina.Aplicacao.Catalogo.Dtos;

public sealed record AtualizarServicoRequest(
    string Nome,
    string Descricao,
    decimal PrecoBase,
    int TempoEstimadoMinutos);
