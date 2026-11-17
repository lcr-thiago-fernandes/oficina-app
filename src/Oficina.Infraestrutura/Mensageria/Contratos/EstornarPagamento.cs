using MassTransit;

namespace Oficina.Infraestrutura.Mensageria.Contratos;

[MessageUrn("oficina:saga:EstornarPagamento")]
[EntityName("oficina-saga-EstornarPagamento")]
[ExcludeFromTopology]
public sealed record EstornarPagamento : IMensagemDaSaga
{
    public Guid OrdemServicoId { get; init; }
    public DateTime OcorridoEm { get; init; }
    public string Motivo { get; init; } = null!;
}
