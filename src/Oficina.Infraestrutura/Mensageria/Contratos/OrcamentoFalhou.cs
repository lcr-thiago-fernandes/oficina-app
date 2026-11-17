using MassTransit;

namespace Oficina.Infraestrutura.Mensageria.Contratos;

[MessageUrn("oficina:saga:OrcamentoFalhou")]
[EntityName("oficina-saga-OrcamentoFalhou")]
public sealed record OrcamentoFalhou : IMensagemDaSaga
{
    public Guid OrdemServicoId { get; init; }
    public DateTime OcorridoEm { get; init; }
    public string Motivo { get; init; } = null!;
}
