using MassTransit;

namespace Oficina.Infraestrutura.Mensageria.Contratos;

[MessageUrn("oficina:saga:GerarOrcamento")]
[EntityName("oficina-saga-GerarOrcamento")]
[ExcludeFromTopology]
public sealed record GerarOrcamento : IMensagemDaSaga
{
    public Guid OrdemServicoId { get; init; }
    public DateTime OcorridoEm { get; init; }
    public long NumeroOs { get; init; }
    public string ClienteDocumento { get; init; } = null!;
    public string ClienteNome { get; init; } = null!;
    public string? ClienteEmail { get; init; }
    public IReadOnlyList<ItemOrcamento> Itens { get; init; } = null!;
}
