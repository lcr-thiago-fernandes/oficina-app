using MassTransit;

namespace Oficina.Infraestrutura.Mensageria.Contratos;

[MessageUrn("oficina:saga:OrcamentoAprovado")]
[EntityName("oficina-saga-OrcamentoAprovado")]
public sealed record OrcamentoAprovado : IMensagemDaSaga
{
    public Guid OrdemServicoId { get; init; }
    public DateTime OcorridoEm { get; init; }
    public Guid OrcamentoId { get; init; }
    public string LinkPagamento { get; init; } = null!;
}
