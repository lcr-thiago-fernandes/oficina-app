namespace Oficina.Dominio.OrdensServico;

public sealed class OrdemDeServico
{
    public const string MotivoRejeicaoPadrao = "Orçamento rejeitado pelo cliente";

    private readonly List<ItemServico> _itensServico = new();
    private readonly List<ItemPeca> _itensPeca = new();
    private readonly List<HistoricoStatus> _historico = new();

    public Guid Id { get; private set; }
    public long Numero { get; private set; }            // BIGSERIAL — preenchido pelo banco
    public Guid ClienteId { get; private set; }
    public Guid VeiculoId { get; private set; }
    public StatusOrdemDeServico Status { get; private set; }
    public string? Observacoes { get; private set; }
    // Sempre "matriz" na Fase 3: nenhum chamador de Criar() informa `unidade`.
    // Ver README, seção "Limitações conhecidas".
    public string Unidade { get; private set; } = "matriz";

    public DateTimeOffset CriadaEm { get; private set; }
    public DateTimeOffset? DiagnosticadaEm { get; private set; }
    public DateTimeOffset? EnviadaAprovacaoEm { get; private set; }
    public DateTimeOffset? OrcamentoAprovadoEm { get; private set; }
    public DateTimeOffset? OrcamentoRejeitadoEm { get; private set; }
    public DateTimeOffset? IniciadaEm { get; private set; }
    public DateTimeOffset? FinalizadaEm { get; private set; }
    public DateTimeOffset? EntregueEm { get; private set; }
    public DateTimeOffset? CanceladaEm { get; private set; }
    public string? MotivoCancelamento { get; private set; }

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

    /// <summary>O atendente só cancela antes de a execução entrar na fila.</summary>
    public bool PodeSerCanceladaPeloAtendente =>
        Status is StatusOrdemDeServico.Recebida
               or StatusOrdemDeServico.AguardandoAprovacao
               or StatusOrdemDeServico.AguardandoPagamento;

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

    public ItemServico AdicionarItemServico(Guid servicoId, string servicoNome, decimal precoSnapshot, int quantidade)
    {
        GarantirEditavel();
        var item = ItemServico.Criar(servicoId, servicoNome, precoSnapshot, quantidade);
        _itensServico.Add(item);
        return item;
    }

    public ItemPeca AdicionarItemPeca(Guid pecaId, string pecaNome, decimal precoSnapshot, int quantidade)
    {
        GarantirEditavel();
        var item = ItemPeca.Criar(pecaId, pecaNome, precoSnapshot, quantidade);
        _itensPeca.Add(item);
        return item;
    }

    public void AguardarAprovacao()
    {
        GarantirOrigem("enviar para aprovação", StatusOrdemDeServico.Recebida);
        if (_itensServico.Count == 0 && _itensPeca.Count == 0)
            throw new OrdemSemItensException();
        EnviadaAprovacaoEm = DateTimeOffset.UtcNow;
        MudarStatus(StatusOrdemDeServico.AguardandoAprovacao);
    }

    public void AguardarPagamento()
    {
        GarantirOrigem("aprovar orçamento", StatusOrdemDeServico.AguardandoAprovacao);
        OrcamentoAprovadoEm = DateTimeOffset.UtcNow;
        MudarStatus(StatusOrdemDeServico.AguardandoPagamento);
    }

    public void Rejeitar(string? motivo = null)
    {
        GarantirOrigem("rejeitar orçamento", StatusOrdemDeServico.AguardandoAprovacao);
        OrcamentoRejeitadoEm = DateTimeOffset.UtcNow;
        MarcarCancelada(string.IsNullOrWhiteSpace(motivo) ? MotivoRejeicaoPadrao : motivo.Trim());
    }

    public void EnfileirarExecucao()
    {
        GarantirOrigem("enfileirar execução", StatusOrdemDeServico.AguardandoPagamento);
        MudarStatus(StatusOrdemDeServico.EmFilaExecucao);
    }

    public void IniciarDiagnostico()
    {
        GarantirOrigem("iniciar diagnóstico", StatusOrdemDeServico.EmFilaExecucao);
        DiagnosticadaEm = DateTimeOffset.UtcNow;
        MudarStatus(StatusOrdemDeServico.EmDiagnostico);
    }

    public void IniciarReparo()
    {
        GarantirOrigem("iniciar reparo",
            StatusOrdemDeServico.EmFilaExecucao,
            StatusOrdemDeServico.EmDiagnostico);
        IniciadaEm = DateTimeOffset.UtcNow;
        MudarStatus(StatusOrdemDeServico.EmReparo);
    }

    public void Finalizar()
    {
        GarantirOrigem("finalizar",
            StatusOrdemDeServico.EmFilaExecucao,
            StatusOrdemDeServico.EmDiagnostico,
            StatusOrdemDeServico.EmReparo);
        FinalizadaEm = DateTimeOffset.UtcNow;
        MudarStatus(StatusOrdemDeServico.Finalizada);
    }

    public void Entregar()
    {
        GarantirOrigem("entregar", StatusOrdemDeServico.Finalizada);
        EntregueEm = DateTimeOffset.UtcNow;
        MudarStatus(StatusOrdemDeServico.Entregue);
    }

    public void Cancelar(string motivo)
    {
        if (string.IsNullOrWhiteSpace(motivo))
            throw new ArgumentException("Motivo do cancelamento obrigatório.", nameof(motivo));
        if (!PodeSerCanceladaPeloAtendente)
            throw new TransicaoDeStatusInvalidaException(Status, "cancelar");
        MarcarCancelada(motivo.Trim());
    }

    private void MarcarCancelada(string motivo)
    {
        MotivoCancelamento = motivo;
        CanceladaEm = DateTimeOffset.UtcNow;
        MudarStatus(StatusOrdemDeServico.Cancelada);
    }

    private void GarantirOrigem(string acao, params StatusOrdemDeServico[] permitidos)
    {
        if (!permitidos.Contains(Status))
            throw new TransicaoDeStatusInvalidaException(Status, acao);
    }

    private void MudarStatus(StatusOrdemDeServico novo)
    {
        var anterior = Status;
        Status = novo;
        RegistrarTransicao(anterior, novo);
    }

    /// <summary>Itens só podem ser incluídos na abertura da OS (status Recebida).</summary>
    private void GarantirEditavel()
    {
        if (Status != StatusOrdemDeServico.Recebida)
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
