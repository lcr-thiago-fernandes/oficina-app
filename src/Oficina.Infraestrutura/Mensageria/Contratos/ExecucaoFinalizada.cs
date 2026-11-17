using MassTransit;

namespace Oficina.Infraestrutura.Mensageria.Contratos;

[MessageUrn("oficina:saga:ExecucaoFinalizada")]
[EntityName("oficina-saga-ExecucaoFinalizada")]
public sealed record ExecucaoFinalizada : IMensagemDaSaga
{
    public Guid OrdemServicoId { get; init; }
    public DateTime OcorridoEm { get; init; }
    public string? Observacao { get; init; }
    public int DuracaoMinutos { get; init; }
}
