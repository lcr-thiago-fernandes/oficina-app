namespace Oficina.Dominio.Estoque;

public sealed class Peca
{
    private readonly List<MovimentacaoEstoque> _movimentacoes = new();

    public Guid Id { get; private set; }
    public Sku Sku { get; private set; } = null!;
    public string Nome { get; private set; } = null!;
    public decimal PrecoUnitario { get; private set; }
    public int SaldoAtual { get; private set; }
    public bool Ativo { get; private set; }
    public DateTimeOffset CriadoEm { get; private set; }

    public IReadOnlyCollection<MovimentacaoEstoque> Movimentacoes => _movimentacoes.AsReadOnly();

    private Peca() { }

    public static Peca Criar(Sku sku, string nome, decimal precoUnitario)
    {
        if (string.IsNullOrWhiteSpace(nome))
            throw new PecaInvalidaException("Nome da peça é obrigatório.");
        if (precoUnitario <= 0m)
            throw new PecaInvalidaException("Preço unitário deve ser maior que zero.");

        return new Peca
        {
            Id = Guid.NewGuid(),
            Sku = sku,
            Nome = nome.Trim(),
            PrecoUnitario = precoUnitario,
            SaldoAtual = 0,
            Ativo = true,
            CriadoEm = DateTimeOffset.UtcNow
        };
    }

    public void AtualizarDados(string nome, decimal precoUnitario)
    {
        if (string.IsNullOrWhiteSpace(nome))
            throw new PecaInvalidaException("Nome da peça é obrigatório.");
        if (precoUnitario <= 0m)
            throw new PecaInvalidaException("Preço unitário deve ser maior que zero.");

        Nome = nome.Trim();
        PrecoUnitario = precoUnitario;
    }

    public void Inativar() => Ativo = false;
    public void Ativar() => Ativo = true;

    public MovimentacaoEstoque RegistrarEntrada(int quantidade, string motivo)
    {
        var mov = MovimentacaoEstoque.Criar(TipoMovimentacao.Entrada, quantidade, motivo, ordemServicoId: null);
        _movimentacoes.Add(mov);
        SaldoAtual += quantidade;
        return mov;
    }

    public MovimentacaoEstoque RegistrarSaida(int quantidade, string motivo, Guid? ordemServicoId)
    {
        // Cria-se primeiro a movimentação para validar quantidade > 0.
        var mov = MovimentacaoEstoque.Criar(TipoMovimentacao.Saida, quantidade, motivo, ordemServicoId);

        if (SaldoAtual < quantidade)
            throw new SaldoInsuficienteException(Sku.Valor, SaldoAtual, quantidade);

        _movimentacoes.Add(mov);
        SaldoAtual -= quantidade;
        return mov;
    }
}
