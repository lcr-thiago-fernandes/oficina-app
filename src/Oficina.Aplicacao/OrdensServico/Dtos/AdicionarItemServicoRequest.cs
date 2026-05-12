namespace Oficina.Aplicacao.OrdensServico.Dtos;

public sealed record AdicionarItemServicoRequest(Guid ServicoId, int Quantidade);
