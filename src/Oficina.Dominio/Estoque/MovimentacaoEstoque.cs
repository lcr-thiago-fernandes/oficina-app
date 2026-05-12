namespace Oficina.Dominio.Estoque;

public sealed class MovimentacaoEstoque
{
    public Guid Id { get; private set; }
    public TipoMovimentacao Tipo { get; private set; }
    public int Quantidade { get; private set; }
    public string Motivo { get; private set; } = null!;
    public Guid? OrdemServicoId { get; private set; }
    public DateTimeOffset CriadoEm { get; private set; }

    private MovimentacaoEstoque() { }

    internal static MovimentacaoEstoque Criar(
        TipoMovimentacao tipo,
        int quantidade,
        string motivo,
        Guid? ordemServicoId)
    {
        if (quantidade <= 0)
            throw new MovimentacaoInvalidaException("A quantidade deve ser maior que zero.");
        if (string.IsNullOrWhiteSpace(motivo))
            throw new MovimentacaoInvalidaException("O motivo é obrigatório.");

        return new MovimentacaoEstoque
        {
            Id = Guid.NewGuid(),
            Tipo = tipo,
            Quantidade = quantidade,
            Motivo = motivo.Trim(),
            OrdemServicoId = ordemServicoId,
            CriadoEm = DateTimeOffset.UtcNow
        };
    }
}
