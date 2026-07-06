namespace Oficina.Aplicacao.OrdensServico.Dtos;

// Corpo do webhook: { "decisao": "aprovado" | "recusado" }.
public sealed record DecisaoOrcamentoRequest(string Decisao);
