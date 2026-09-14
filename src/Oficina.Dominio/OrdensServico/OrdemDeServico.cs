namespace Oficina.Dominio.OrdensServico;

public sealed class OrdemDeServico
{
    private readonly List<ItemServico> _itensServico = new();
    private readonly List<ItemPeca> _itensPeca = new();
    private readonly List<HistoricoStatus> _historico = new();

    public Guid Id { get; private set; }
    public long Numero { get; private set; }            // BIGSERIAL — preenchido pelo banco
    public Guid ClienteId { get; private set; }
    public Guid VeiculoId { get; private set; }
    public StatusOrdemDeServico Status { get; private set; }
    public string? Observacoes { get; private set; }
    public string Unidade { get; private set; } = "matriz";

    public DateTimeOffset CriadaEm { get; private set; }
    public DateTimeOffset? DiagnosticadaEm { get; private set; }
    public DateTimeOffset? EnviadaAprovacaoEm { get; private set; }
    public DateTimeOffset? OrcamentoAprovadoEm { get; private set; }
    public DateTimeOffset? OrcamentoRejeitadoEm { get; private set; }
    public DateTimeOffset? IniciadaEm { get; private set; }
    public DateTimeOffset? FinalizadaEm { get; private set; }
    public DateTimeOffset? EntregueEm { get; private set; }

    public IReadOnlyCollection<ItemServico> ItensServico => _itensServico.AsReadOnly();
    public IReadOnlyCollection<ItemPeca> ItensPeca => _itensPeca.AsReadOnly();
    public IReadOnlyCollection<HistoricoStatus> Historico => _historico.AsReadOnly();

    public decimal TotalServicos => _itensServico.Sum(i => i.Subtotal);
    public decimal TotalPecas => _itensPeca.Sum(i => i.Subtotal);
    public decimal TotalGeral => TotalServicos + TotalPecas;
    public TimeSpan? DuracaoExecucao =>
        IniciadaEm.HasValue && FinalizadaEm.HasValue
            ? FinalizadaEm.Value - IniciadaEm.Value
            : null;

    private OrdemDeServico() { }

    public static OrdemDeServico Criar(
        Guid clienteId, Guid veiculoId, string? observacoes = null, string unidade = "matriz")
    {
        if (clienteId == Guid.Empty)
            throw new ArgumentException("ClienteId obrigatório.", nameof(clienteId));
        if (veiculoId == Guid.Empty)
            throw new ArgumentException("VeiculoId obrigatório.", nameof(veiculoId));
        if (string.IsNullOrWhiteSpace(unidade))
            throw new ArgumentException("Unidade obrigatória.", nameof(unidade));

        var os = new OrdemDeServico
        {
            Id = Guid.NewGuid(),
            ClienteId = clienteId,
            VeiculoId = veiculoId,
            Status = StatusOrdemDeServico.Recebida,
            CriadaEm = DateTimeOffset.UtcNow,
            Observacoes = observacoes?.Trim(),
            Unidade = unidade.Trim()
        };
        os.RegistrarTransicao(null, StatusOrdemDeServico.Recebida);
        return os;
    }

    public void IniciarDiagnostico()
    {
        if (Status != StatusOrdemDeServico.Recebida)
            throw new TransicaoDeStatusInvalidaException(Status, "iniciar diagnóstico");
        Status = StatusOrdemDeServico.EmDiagnostico;
        DiagnosticadaEm = DateTimeOffset.UtcNow;
        RegistrarTransicao(StatusOrdemDeServico.Recebida, StatusOrdemDeServico.EmDiagnostico);
    }

    public ItemServico AdicionarItemServico(Guid servicoId, string servicoNome, decimal precoSnapshot, int quantidade)
    {
        GarantirEditavel();
        var item = ItemServico.Criar(servicoId, servicoNome, precoSnapshot, quantidade);
        _itensServico.Add(item);
        return item;
    }

    public void RemoverItemServico(Guid itemId)
    {
        GarantirEditavel();
        var item = _itensServico.FirstOrDefault(i => i.Id == itemId)
            ?? throw new ItemNaoEncontradoException("serviço", itemId);
        _itensServico.Remove(item);
    }

    public ItemPeca AdicionarItemPeca(Guid pecaId, string pecaNome, decimal precoSnapshot, int quantidade)
    {
        GarantirEditavel();
        var item = ItemPeca.Criar(pecaId, pecaNome, precoSnapshot, quantidade);
        _itensPeca.Add(item);
        return item;
    }

    public void RemoverItemPeca(Guid itemId)
    {
        GarantirEditavel();
        var item = _itensPeca.FirstOrDefault(i => i.Id == itemId)
            ?? throw new ItemNaoEncontradoException("peça", itemId);
        _itensPeca.Remove(item);
    }

    public void EnviarOrcamentoParaAprovacao()
    {
        if (Status != StatusOrdemDeServico.EmDiagnostico)
            throw new TransicaoDeStatusInvalidaException(Status, "enviar para aprovação");
        if (_itensServico.Count == 0 && _itensPeca.Count == 0)
            throw new OrdemSemItensException();
        Status = StatusOrdemDeServico.AguardandoAprovacao;
        EnviadaAprovacaoEm = DateTimeOffset.UtcNow;
        RegistrarTransicao(StatusOrdemDeServico.EmDiagnostico, StatusOrdemDeServico.AguardandoAprovacao);
    }

    public void Aprovar()
    {
        if (Status != StatusOrdemDeServico.AguardandoAprovacao)
            throw new TransicaoDeStatusInvalidaException(Status, "aprovar orçamento");
        OrcamentoAprovadoEm = DateTimeOffset.UtcNow;
    }

    public void Rejeitar()
    {
        if (Status != StatusOrdemDeServico.AguardandoAprovacao)
            throw new TransicaoDeStatusInvalidaException(Status, "rejeitar orçamento");
        OrcamentoRejeitadoEm = DateTimeOffset.UtcNow;
        Status = StatusOrdemDeServico.Cancelada;
        RegistrarTransicao(StatusOrdemDeServico.AguardandoAprovacao, StatusOrdemDeServico.Cancelada);
    }

    public void IniciarExecucao()
    {
        if (Status != StatusOrdemDeServico.AguardandoAprovacao)
            throw new TransicaoDeStatusInvalidaException(Status, "iniciar execução");
        if (OrcamentoAprovadoEm is null)
            throw new OrcamentoNaoAprovadoException();

        Status = StatusOrdemDeServico.EmExecucao;
        IniciadaEm = DateTimeOffset.UtcNow;
        RegistrarTransicao(StatusOrdemDeServico.AguardandoAprovacao, StatusOrdemDeServico.EmExecucao);
    }

    public void Finalizar()
    {
        if (Status != StatusOrdemDeServico.EmExecucao)
            throw new TransicaoDeStatusInvalidaException(Status, "finalizar");
        Status = StatusOrdemDeServico.Finalizada;
        FinalizadaEm = DateTimeOffset.UtcNow;
        RegistrarTransicao(StatusOrdemDeServico.EmExecucao, StatusOrdemDeServico.Finalizada);
    }

    public void Entregar()
    {
        if (Status != StatusOrdemDeServico.Finalizada)
            throw new TransicaoDeStatusInvalidaException(Status, "entregar");
        Status = StatusOrdemDeServico.Entregue;
        EntregueEm = DateTimeOffset.UtcNow;
        RegistrarTransicao(StatusOrdemDeServico.Finalizada, StatusOrdemDeServico.Entregue);
    }

    private void GarantirEditavel()
    {
        if (Status is StatusOrdemDeServico.EmExecucao
                   or StatusOrdemDeServico.Finalizada
                   or StatusOrdemDeServico.Entregue
                   or StatusOrdemDeServico.Cancelada)
            throw new OrdemImutavelException();
    }

    /// <summary>
    /// Acrescenta uma entrada ao histórico. A duração é o tempo decorrido
    /// desde a última transição registrada — na primeira, é nula.
    /// </summary>
    private void RegistrarTransicao(StatusOrdemDeServico? anterior, StatusOrdemDeServico novo)
    {
        var agora = DateTimeOffset.UtcNow;
        long? duracao = null;

        var ultima = _historico.LastOrDefault();
        if (ultima is not null)
            duracao = (long)(agora - ultima.OcorridoEm).TotalSeconds;

        _historico.Add(HistoricoStatus.Criar(anterior, novo, agora, duracao));
    }
}
