using MassTransit;

namespace Oficina.Infraestrutura.Mensageria.Contratos;

[MessageUrn("oficina:saga:OrcamentoRejeitado")]
[EntityName("oficina-saga-OrcamentoRejeitado")]
public sealed record OrcamentoRejeitado : IMensagemDaSaga
{
    public Guid OrdemServicoId { get; init; }
    public DateTime OcorridoEm { get; init; }
    public Guid OrcamentoId { get; init; }
    public string? Motivo { get; init; }
}
