using MassTransit;

namespace Oficina.Infraestrutura.Mensageria.Contratos;

[MessageUrn("oficina:saga:StatusExecucaoAlterado")]
[EntityName("oficina-saga-StatusExecucaoAlterado")]
public sealed record StatusExecucaoAlterado : IMensagemDaSaga
{
    public Guid OrdemServicoId { get; init; }
    public DateTime OcorridoEm { get; init; }
    public StatusExecucao StatusExecucao { get; init; }
    public string? Observacao { get; init; }
}
