namespace Oficina.Dominio.OrdensServico;

public sealed class ItemServico
{
    public Guid Id { get; private set; }
    public Guid ServicoId { get; private set; }
    public string ServicoNome { get; private set; } = null!;
    public decimal PrecoSnapshot { get; private set; }
    public int Quantidade { get; private set; }

    public decimal Subtotal => PrecoSnapshot * Quantidade;

    private ItemServico() { }

    internal static ItemServico Criar(Guid servicoId, string servicoNome, decimal precoSnapshot, int quantidade)
    {
        if (string.IsNullOrWhiteSpace(servicoNome))
            throw new ItemInvalidoException("Nome do serviço é obrigatório no item.");
        if (precoSnapshot <= 0m)
            throw new ItemInvalidoException("Preço do serviço deve ser maior que zero.");
        if (quantidade <= 0)
            throw new ItemInvalidoException("Quantidade do serviço deve ser maior que zero.");

        return new ItemServico
        {
            Id = Guid.NewGuid(),
            ServicoId = servicoId,
            ServicoNome = servicoNome.Trim(),
            PrecoSnapshot = precoSnapshot,
            Quantidade = quantidade
        };
    }
}
