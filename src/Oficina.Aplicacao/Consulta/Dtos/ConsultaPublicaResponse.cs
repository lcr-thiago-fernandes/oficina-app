namespace Oficina.Aplicacao.Consulta.Dtos;

public sealed record ConsultaPublicaResponse(
    long Numero,
    string Status,
    string ClienteNome,
    string DocumentoMascarado,
    string VeiculoPlaca,
    string VeiculoMarcaModelo,
    decimal TotalServicos,
    decimal TotalPecas,
    decimal TotalGeral,
    DateTimeOffset CriadaEm,
    DateTimeOffset? EnviadaAprovacaoEm,
    DateTimeOffset? OrcamentoAprovadoEm,
    DateTimeOffset? OrcamentoRejeitadoEm,
    DateTimeOffset? IniciadaEm,
    DateTimeOffset? FinalizadaEm,
    DateTimeOffset? EntregueEm,
    IReadOnlyList<ItemConsultaResponse> Itens);
