using Oficina.Aplicacao.OrdensServico.Saga.Telemetria;
using Oficina.Infraestrutura.Persistencia;

namespace Oficina.Infraestrutura.Mensageria.Saga;

/// <summary>
/// Trilha da saga no PostgreSQL: só adiciona o passo ao DbContext do consumo; ele é gravado junto com o
/// estado da saga e as mensagens do outbox, na mesma transação.
/// </summary>
public sealed class RegistradorPassoSaga : IRegistradorPassoSaga
{
    private readonly OficinaDbContext _db;
    private readonly IPublicadorEventoSaga _publicador;

    public RegistradorPassoSaga(OficinaDbContext db, IPublicadorEventoSaga publicador)
    {
        _db = db;
        _publicador = publicador;
    }

    public void Registrar(EstadoSagaOrdemServico saga, string passo, string resultado, bool compensacao, string? detalhe = null, string? motivo = null)
    {
        var agora = DateTime.UtcNow;
        // Antes de avançar AtualizadaEm: a duração é o tempo desde o passo anterior.
        _publicador.Publicar(new EventoSagaOrdemServico(
            saga.CorrelationId, saga.NumeroOs, passo, resultado, compensacao,
            (long)(agora - saga.AtualizadaEm).TotalMilliseconds, saga.EstadoAtual, motivo ?? MotivoDeNegocio(saga, resultado, compensacao), detalhe));
        _db.Set<PassoSaga>().Add(new PassoSaga
        {
            Id = Guid.NewGuid(),
            OrdemServicoId = saga.CorrelationId,
            Passo = passo,
            Resultado = resultado,
            Compensacao = compensacao,
            Detalhe = detalhe,
            Estado = saga.EstadoAtual,
            OcorridoEm = agora,
        });
        saga.AtualizadaEm = agora;
    }

    /// <summary>
    /// Razão de negócio do passo: sempre presente em compensações e falhas (a que a saga guardou em
    /// <c>Motivo</c>); passos de sucesso não têm.
    /// </summary>
    internal static string? MotivoDeNegocio(EstadoSagaOrdemServico saga, string resultado, bool compensacao) =>
        compensacao || resultado is PassosSaga.Falha or PassosSaga.CompensacaoFalhou ? saga.Motivo : null;
}
