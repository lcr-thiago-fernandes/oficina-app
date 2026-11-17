using MassTransit;

namespace Oficina.Infraestrutura.Mensageria.Contratos;

[MessageUrn("oficina:saga:PagamentoRecusado")]
[EntityName("oficina-saga-PagamentoRecusado")]
public sealed record PagamentoRecusado : IMensagemDaSaga
{
    public Guid OrdemServicoId { get; init; }
    public DateTime OcorridoEm { get; init; }
    public string? PagamentoId { get; init; }
    public string Motivo { get; init; } = null!;
}
