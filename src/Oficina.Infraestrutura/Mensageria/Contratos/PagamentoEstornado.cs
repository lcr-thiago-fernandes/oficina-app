using MassTransit;

namespace Oficina.Infraestrutura.Mensageria.Contratos;

[MessageUrn("oficina:saga:PagamentoEstornado")]
[EntityName("oficina-saga-PagamentoEstornado")]
public sealed record PagamentoEstornado : IMensagemDaSaga
{
    public Guid OrdemServicoId { get; init; }
    public DateTime OcorridoEm { get; init; }
    public string PagamentoId { get; init; } = null!;
    public decimal Valor { get; init; }
}
