using MassTransit;
using Oficina.Infraestrutura.Mensageria.Contratos;

namespace Oficina.Infraestrutura.Mensageria.Internos;

/// <summary>Evento interno (fora dos 16 contratos): o atendente pediu o cancelamento da OS.</summary>
[MessageUrn("oficina:os:CancelamentoSolicitado")]
[EntityName("oficina-os-CancelamentoSolicitado")]
public sealed record CancelamentoSolicitado : IMensagemDaSaga
{
    public Guid OrdemServicoId { get; init; }
    public DateTime OcorridoEm { get; init; }
    public string Motivo { get; init; } = null!;
}
