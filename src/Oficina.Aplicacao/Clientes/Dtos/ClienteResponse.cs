namespace Oficina.Aplicacao.Clientes.Dtos;

public sealed record ClienteResponse(
    Guid Id,
    string Nome,
    string TipoPessoa,
    string Documento,
    string DocumentoMascarado,
    string Email,
    string Telefone,
    bool Ativo,
    DateTimeOffset CriadoEm,
    IReadOnlyList<VeiculoResponse> Veiculos);
