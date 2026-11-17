using MassTransit;

namespace Oficina.Infraestrutura.Mensageria.Contratos;

[MessageUrn("oficina:saga:ExecucaoRecusada")]
[EntityName("oficina-saga-ExecucaoRecusada")]
public sealed record ExecucaoRecusada : IMensagemDaSaga
{
    public Guid OrdemServicoId { get; init; }
    public DateTime OcorridoEm { get; init; }
    public string Motivo { get; init; } = null!;
}
