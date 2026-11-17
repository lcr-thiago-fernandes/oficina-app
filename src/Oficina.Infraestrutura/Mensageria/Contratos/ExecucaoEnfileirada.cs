using MassTransit;

namespace Oficina.Infraestrutura.Mensageria.Contratos;

[MessageUrn("oficina:saga:ExecucaoEnfileirada")]
[EntityName("oficina-saga-ExecucaoEnfileirada")]
public sealed record ExecucaoEnfileirada : IMensagemDaSaga
{
    public Guid OrdemServicoId { get; init; }
    public DateTime OcorridoEm { get; init; }
    public int PosicaoFila { get; init; }
}
