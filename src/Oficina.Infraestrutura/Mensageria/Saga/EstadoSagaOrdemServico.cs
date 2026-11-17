using MassTransit;

namespace Oficina.Infraestrutura.Mensageria.Saga;

/// <summary>Instância persistida da saga de uma OS (<c>CorrelationId = OrdemServicoId</c>).</summary>
public class EstadoSagaOrdemServico : SagaStateMachineInstance
{
    public Guid CorrelationId { get; set; }
    public string EstadoAtual { get; set; } = null!;
    public long NumeroOs { get; set; }
    public string? PagamentoId { get; set; }
    public string? Motivo { get; set; }
    public DateTime CriadaEm { get; set; }
    public DateTime AtualizadaEm { get; set; }

    /// <summary>Concorrência otimista (mapeada para <c>xmin</c> na Task 6).</summary>
    public uint Versao { get; set; }
}
