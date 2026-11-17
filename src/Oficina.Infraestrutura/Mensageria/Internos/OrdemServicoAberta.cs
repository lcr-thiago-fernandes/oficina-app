using MassTransit;
using Oficina.Infraestrutura.Mensageria.Contratos;

namespace Oficina.Infraestrutura.Mensageria.Internos;

/// <summary>Evento interno (fora dos 16 contratos): a OS foi aberta e a saga deve começar.</summary>
[MessageUrn("oficina:os:OrdemServicoAberta")]
[EntityName("oficina-os-OrdemServicoAberta")]
public sealed record OrdemServicoAberta : IMensagemDaSaga
{
    public Guid OrdemServicoId { get; init; }
    public DateTime OcorridoEm { get; init; }
}
