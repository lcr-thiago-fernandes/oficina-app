namespace Oficina.Infraestrutura.Mensageria.Saga;

/// <summary>Um passo da trilha da saga (tabela <c>saga.ordem_servico_saga_passo</c>).</summary>
public class PassoSaga
{
    public Guid Id { get; set; }
    public Guid OrdemServicoId { get; set; }
    public string Passo { get; set; } = null!;
    public string Resultado { get; set; } = null!;
    public bool Compensacao { get; set; }
    public string? Detalhe { get; set; }

    /// <summary>Estado da saga <b>depois</b> deste passo.</summary>
    public string Estado { get; set; } = null!;

    public DateTime OcorridoEm { get; set; }
}
