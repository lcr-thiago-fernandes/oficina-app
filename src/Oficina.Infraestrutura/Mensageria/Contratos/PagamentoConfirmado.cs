using MassTransit;

namespace Oficina.Infraestrutura.Mensageria.Contratos;

[MessageUrn("oficina:saga:PagamentoConfirmado")]
[EntityName("oficina-saga-PagamentoConfirmado")]
public sealed record PagamentoConfirmado : IMensagemDaSaga
{
    public Guid OrdemServicoId { get; init; }
    public DateTime OcorridoEm { get; init; }
    public string PagamentoId { get; init; } = null!;
    public decimal Valor { get; init; }
    public string Meio { get; init; } = null!;
}
