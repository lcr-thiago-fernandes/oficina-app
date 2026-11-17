namespace Oficina.Infraestrutura.Mensageria.Contratos;

public sealed record ItemExecucao
{
    public string Descricao { get; init; } = null!;
    public int Quantidade { get; init; }
}
