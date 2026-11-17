namespace Oficina.Dominio.Estoque;

public sealed class Peca
{
    private readonly List<MovimentacaoEstoque> _movimentacoes = new();

    public Guid Id { get; private set; }
    public Sku Sku { get; private set; } = null!;
    public string Nome { get; private set; } = null!;
    public decimal PrecoUnitario { get; private set; }
    public int SaldoAtual { get; private set; }

    /// <summary>Unidades reservadas por OSs ainda não confirmadas (lock semântico da saga).</summary>
    public int SaldoReservado { get; private set; }

    public int SaldoDisponivel => SaldoAtual - SaldoReservado;
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

        // A baixa de uma OS com reserva é feita só por ConfirmarReserva: uma saída manual com o mesmo
        // OrdemServicoId passaria a contar como confirmação sem baixar o reservado.
        if (ordemServicoId is Guid os && SituacaoReserva(os) != SituacaoReservaPeca.Nenhuma)
            throw new ReservaInvalidaException(
                $"A OS {os} tem reserva na peça '{Sku.Valor}': a baixa é feita pela confirmação da reserva.");

        // Unidades reservadas são intocáveis pela saída manual.
        if (SaldoDisponivel < quantidade)
            throw new SaldoInsuficienteException(Sku.Valor, SaldoDisponivel, quantidade);

        _movimentacoes.Add(mov);
        SaldoAtual -= quantidade;
        return mov;
    }

    public MovimentacaoEstoque Reservar(int quantidade, Guid ordemServicoId)
    {
        var situacao = SituacaoReserva(ordemServicoId);
        if (situacao != SituacaoReservaPeca.Nenhuma)
            throw new ReservaInvalidaException(
                $"A OS {ordemServicoId} já tem movimento de reserva na peça '{Sku.Valor}' (situação {situacao}).");

        var mov = MovimentacaoEstoque.Criar(TipoMovimentacao.Reserva, quantidade, "Reserva para a OS", ordemServicoId);

        if (SaldoDisponivel < quantidade)
            throw new SaldoInsuficienteException(Sku.Valor, SaldoDisponivel, quantidade);

        _movimentacoes.Add(mov);
        SaldoReservado += quantidade;
        return mov;
    }

    /// <summary>Baixa a reserva da OS. Idempotente: já confirmada → <c>null</c>.</summary>
    public MovimentacaoEstoque? ConfirmarReserva(Guid ordemServicoId)
    {
        var situacao = SituacaoReserva(ordemServicoId);
        if (situacao == SituacaoReservaPeca.Confirmada) return null;
        if (situacao != SituacaoReservaPeca.Reservada)
            throw SituacaoInvalida("confirmar a reserva", ordemServicoId, situacao);

        var quantidade = QuantidadeReservada(ordemServicoId);
        var mov = MovimentacaoEstoque.Criar(TipoMovimentacao.Saida, quantidade, "Baixa da reserva da OS", ordemServicoId);
        _movimentacoes.Add(mov);
        SaldoReservado -= quantidade;
        SaldoAtual -= quantidade;
        return mov;
    }

    /// <summary>Devolve a reserva ao disponível. Idempotente: liberada ou sem reserva → <c>null</c>.</summary>
    public MovimentacaoEstoque? LiberarReserva(Guid ordemServicoId)
    {
        var situacao = SituacaoReserva(ordemServicoId);
        if (situacao is SituacaoReservaPeca.Liberada or SituacaoReservaPeca.Nenhuma) return null;
        if (situacao != SituacaoReservaPeca.Reservada)
            throw SituacaoInvalida("liberar a reserva", ordemServicoId, situacao);

        var quantidade = QuantidadeReservada(ordemServicoId);
        var mov = MovimentacaoEstoque.Criar(TipoMovimentacao.LiberacaoReserva, quantidade, "Liberação da reserva da OS", ordemServicoId);
        _movimentacoes.Add(mov);
        SaldoReservado -= quantidade;
        return mov;
    }

    /// <summary>Compensa a baixa confirmada da OS. Idempotente: já reposta → <c>null</c>.</summary>
    public MovimentacaoEstoque? Repor(Guid ordemServicoId)
    {
        var situacao = SituacaoReserva(ordemServicoId);
        if (situacao == SituacaoReservaPeca.Reposta) return null;
        if (situacao != SituacaoReservaPeca.Confirmada)
            throw SituacaoInvalida("repor", ordemServicoId, situacao);

        var quantidade = QuantidadeReservada(ordemServicoId);
        var mov = MovimentacaoEstoque.Criar(TipoMovimentacao.Reposicao, quantidade, "Reposição da baixa da OS", ordemServicoId);
        _movimentacoes.Add(mov);
        SaldoAtual += quantidade;
        return mov;
    }

    /// <summary>
    /// Situação = último movimento da OS entre Reserva/Saida/LiberacaoReserva/Reposicao, ordenado por
    /// <c>CriadoEm</c> com desempate pela precedência do tipo. Nunca pela ordem da coleção: o
    /// <c>Include</c> do EF não garante ordem, e o PostgreSQL trunca <c>CriadoEm</c> a microssegundos.
    /// </summary>
    public SituacaoReservaPeca SituacaoReserva(Guid ordemServicoId)
    {
        var ultimo = _movimentacoes
            .Where(m => m.OrdemServicoId == ordemServicoId && PrecedenciaReserva(m.Tipo) >= 0)
            .OrderBy(m => m.CriadoEm)
            .ThenBy(m => PrecedenciaReserva(m.Tipo))
            .LastOrDefault();

        return ultimo?.Tipo switch
        {
            TipoMovimentacao.Reserva => SituacaoReservaPeca.Reservada,
            TipoMovimentacao.Saida => SituacaoReservaPeca.Confirmada,
            TipoMovimentacao.LiberacaoReserva => SituacaoReservaPeca.Liberada,
            TipoMovimentacao.Reposicao => SituacaoReservaPeca.Reposta,
            _ => SituacaoReservaPeca.Nenhuma
        };
    }

    private static int PrecedenciaReserva(TipoMovimentacao tipo) => tipo switch
    {
        TipoMovimentacao.Reserva => 0,
        TipoMovimentacao.Saida or TipoMovimentacao.LiberacaoReserva => 1,
        TipoMovimentacao.Reposicao => 2,
        _ => -1
    };

    private int QuantidadeReservada(Guid ordemServicoId) =>
        _movimentacoes.FirstOrDefault(m => m.OrdemServicoId == ordemServicoId && m.Tipo == TipoMovimentacao.Reserva)?.Quantidade
        // Saída manual antiga (Fase 3) com OrdemServicoId, sem reserva: não há o que compensar.
        ?? throw new ReservaInvalidaException(
            $"A OS {ordemServicoId} não tem reserva registrada na peça '{Sku.Valor}'.");

    private ReservaInvalidaException SituacaoInvalida(string operacao, Guid ordemServicoId, SituacaoReservaPeca situacao) =>
        new($"Não é possível {operacao} da OS {ordemServicoId} na peça '{Sku.Valor}': situação {situacao}.");
}
