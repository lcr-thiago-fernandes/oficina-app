using MassTransit;

namespace Oficina.Infraestrutura.Mensageria.Contratos;

[MessageUrn("oficina:saga:IniciarExecucao")]
[EntityName("oficina-saga-IniciarExecucao")]
[ExcludeFromTopology]
public sealed record IniciarExecucao : IMensagemDaSaga
{
    public Guid OrdemServicoId { get; init; }
    public DateTime OcorridoEm { get; init; }
    public long NumeroOs { get; init; }
    public string Unidade { get; init; } = null!;
    public IReadOnlyList<ItemExecucao> Servicos { get; init; } = null!;
    public IReadOnlyList<ItemExecucao> Pecas { get; init; } = null!;
}
