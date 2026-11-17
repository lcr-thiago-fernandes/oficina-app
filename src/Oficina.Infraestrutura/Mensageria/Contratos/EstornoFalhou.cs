using MassTransit;

namespace Oficina.Infraestrutura.Mensageria.Contratos;

[MessageUrn("oficina:saga:EstornoFalhou")]
[EntityName("oficina-saga-EstornoFalhou")]
public sealed record EstornoFalhou : IMensagemDaSaga
{
    public Guid OrdemServicoId { get; init; }
    public DateTime OcorridoEm { get; init; }
    public string Motivo { get; init; } = null!;
}
