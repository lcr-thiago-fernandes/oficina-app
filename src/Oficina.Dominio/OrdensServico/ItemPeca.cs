namespace Oficina.Dominio.OrdensServico;

public sealed class ItemPeca
{
    public Guid Id { get; private set; }
    public Guid PecaId { get; private set; }
    public string PecaNome { get; private set; } = null!;
    public decimal PrecoSnapshot { get; private set; }
    public int Quantidade { get; private set; }

    public decimal Subtotal => PrecoSnapshot * Quantidade;

    private ItemPeca() { }

    internal static ItemPeca Criar(Guid pecaId, string pecaNome, decimal precoSnapshot, int quantidade)
    {
        if (string.IsNullOrWhiteSpace(pecaNome))
            throw new ItemInvalidoException("Nome da peça é obrigatório no item.");
        if (precoSnapshot <= 0m)
            throw new ItemInvalidoException("Preço da peça deve ser maior que zero.");
        if (quantidade <= 0)
            throw new ItemInvalidoException("Quantidade da peça deve ser maior que zero.");

        return new ItemPeca
        {
            Id = Guid.NewGuid(),
            PecaId = pecaId,
            PecaNome = pecaNome.Trim(),
            PrecoSnapshot = precoSnapshot,
            Quantidade = quantidade
        };
    }
}
