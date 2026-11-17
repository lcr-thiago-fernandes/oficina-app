using MassTransit;

namespace Oficina.Infraestrutura.Mensageria.Contratos;

[MessageUrn("oficina:saga:CancelarOrcamento")]
[EntityName("oficina-saga-CancelarOrcamento")]
[ExcludeFromTopology]
public sealed record CancelarOrcamento : IMensagemDaSaga
{
    public Guid OrdemServicoId { get; init; }
    public DateTime OcorridoEm { get; init; }
    public string Motivo { get; init; } = null!;
}
