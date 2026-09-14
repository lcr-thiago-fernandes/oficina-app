namespace Oficina.Dominio.OrdensServico;

/// <summary>
/// Registro imutável de uma transição da máquina de estados da OS.
/// É a fonte de verdade do dashboard "tempo médio por status" (Fase 3):
/// os timestamps do agregado dizem QUANDO cada marco ocorreu, mas não
/// permitem reconstruir a permanência em cada estado de forma uniforme.
/// </summary>
public sealed class HistoricoStatus
{
    public Guid Id { get; private set; }
    public StatusOrdemDeServico? StatusAnterior { get; private set; }
    public StatusOrdemDeServico StatusNovo { get; private set; }
    public DateTimeOffset OcorridoEm { get; private set; }
    public long? DuracaoSegundos { get; private set; }
    public Guid? UsuarioId { get; private set; }

    private HistoricoStatus() { }

    internal static HistoricoStatus Criar(
        StatusOrdemDeServico? statusAnterior,
        StatusOrdemDeServico statusNovo,
        DateTimeOffset ocorridoEm,
        long? duracaoSegundos,
        Guid? usuarioId = null) => new()
        {
            Id = Guid.NewGuid(),
            StatusAnterior = statusAnterior,
            StatusNovo = statusNovo,
            OcorridoEm = ocorridoEm,
            DuracaoSegundos = duracaoSegundos,
            UsuarioId = usuarioId
        };
}
