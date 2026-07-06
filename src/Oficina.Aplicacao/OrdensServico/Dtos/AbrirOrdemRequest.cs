namespace Oficina.Aplicacao.OrdensServico.Dtos;

public sealed record AbrirOrdemRequest(
    ClienteDadosDto ClienteDados,
    VeiculoDadosDto VeiculoDados,
    IReadOnlyList<ItemServicoDto> Servicos,
    IReadOnlyList<ItemPecaDto> Pecas,
    string? Observacoes = null);

public sealed record ClienteDadosDto(string Documento, string Nome, string Email, string Telefone);

public sealed record VeiculoDadosDto(string Placa, string Marca, string Modelo, int Ano);

public sealed record ItemServicoDto(Guid ServicoId, int Quantidade);

public sealed record ItemPecaDto(Guid PecaId, int Quantidade);
