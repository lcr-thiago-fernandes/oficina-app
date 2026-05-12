namespace Oficina.Dominio.OrdensServico;

public sealed class OrdemDeServico
{
    private readonly List<ItemServico> _itensServico = new();
    private readonly List<ItemPeca> _itensPeca = new();
    private readonly List<EventoOs> _eventos = new();

    public Guid Id { get; private set; }
    public long Numero { get; private set; }            // BIGSERIAL — preenchido pelo banco
    public Guid ClienteId { get; private set; }
    public Guid VeiculoId { get; private set; }
    public StatusOrdemDeServico Status { get; private set; }
    public string? Observacoes { get; private set; }

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
    public IReadOnlyList<EventoOs> EventosNaoPublicados => _eventos.AsReadOnly();

    public decimal TotalServicos => _itensServico.Sum(i => i.Subtotal);
    public decimal TotalPecas => _itensPeca.Sum(i => i.Subtotal);
    public decimal TotalGeral => TotalServicos + TotalPecas;
    public TimeSpan? DuracaoExecucao =>
        IniciadaEm.HasValue && FinalizadaEm.HasValue
            ? FinalizadaEm.Value - IniciadaEm.Value
            : null;

    private OrdemDeServico() { }

    public static OrdemDeServico Criar(Guid clienteId, Guid veiculoId, string? observacoes = null)
    {
        if (clienteId == Guid.Empty)
            throw new ArgumentException("ClienteId obrigatório.", nameof(clienteId));
        if (veiculoId == Guid.Empty)
            throw new ArgumentException("VeiculoId obrigatório.", nameof(veiculoId));

        var os = new OrdemDeServico
        {
            Id = Guid.NewGuid(),
            ClienteId = clienteId,
            VeiculoId = veiculoId,
            Status = StatusOrdemDeServico.Recebida,
            CriadaEm = DateTimeOffset.UtcNow,
            Observacoes = observacoes?.Trim()
        };
        os._eventos.Add(new OrdemDeServicoCriadaEvent(os.Id, os.CriadaEm));
        return os;
    }

    public void IniciarDiagnostico()
    {
        if (Status != StatusOrdemDeServico.Recebida)
            throw new TransicaoDeStatusInvalidaException(Status, "iniciar diagnóstico");
        Status = StatusOrdemDeServico.EmDiagnostico;
        DiagnosticadaEm = DateTimeOffset.UtcNow;
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
    }

    public void IniciarExecucao()
    {
        if (Status != StatusOrdemDeServico.AguardandoAprovacao)
            throw new TransicaoDeStatusInvalidaException(Status, "iniciar execução");
        if (OrcamentoAprovadoEm is null)
            throw new OrcamentoNaoAprovadoException();

        Status = StatusOrdemDeServico.EmExecucao;
        IniciadaEm = DateTimeOffset.UtcNow;

        var pecas = _itensPeca
            .Select(i => (i.PecaId, i.Quantidade))
            .ToList();
        _eventos.Add(new ExecucaoIniciadaEvent(Id, pecas, IniciadaEm.Value));
    }

    public void Finalizar()
    {
        if (Status != StatusOrdemDeServico.EmExecucao)
            throw new TransicaoDeStatusInvalidaException(Status, "finalizar");
        Status = StatusOrdemDeServico.Finalizada;
        FinalizadaEm = DateTimeOffset.UtcNow;
        _eventos.Add(new OrdemFinalizadaEvent(Id, FinalizadaEm.Value));
    }

    public void Entregar()
    {
        if (Status != StatusOrdemDeServico.Finalizada)
            throw new TransicaoDeStatusInvalidaException(Status, "entregar");
        Status = StatusOrdemDeServico.Entregue;
        EntregueEm = DateTimeOffset.UtcNow;
        _eventos.Add(new OrdemEntregueEvent(Id, EntregueEm.Value));
    }

    public void LimparEventos() => _eventos.Clear();

    private void GarantirEditavel()
    {
        if (Status is StatusOrdemDeServico.EmExecucao
                   or StatusOrdemDeServico.Finalizada
                   or StatusOrdemDeServico.Entregue
                   or StatusOrdemDeServico.Cancelada)
            throw new OrdemImutavelException();
    }
}
