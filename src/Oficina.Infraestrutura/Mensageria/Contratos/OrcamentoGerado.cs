using MassTransit;

namespace Oficina.Infraestrutura.Mensageria.Contratos;

[MessageUrn("oficina:saga:OrcamentoGerado")]
[EntityName("oficina-saga-OrcamentoGerado")]
public sealed record OrcamentoGerado : IMensagemDaSaga
{
    public Guid OrdemServicoId { get; init; }
    public DateTime OcorridoEm { get; init; }
    public Guid OrcamentoId { get; init; }
    public decimal ValorTotal { get; init; }
}
