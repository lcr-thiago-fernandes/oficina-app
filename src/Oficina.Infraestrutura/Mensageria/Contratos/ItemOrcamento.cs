namespace Oficina.Infraestrutura.Mensageria.Contratos;

public sealed record ItemOrcamento
{
    public TipoItemOrcamento Tipo { get; init; }
    public Guid ReferenciaId { get; init; }
    public string Descricao { get; init; } = null!;
    public int Quantidade { get; init; }
    public decimal PrecoUnitario { get; init; }
}
