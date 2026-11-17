using Oficina.Infraestrutura.Persistencia;

namespace Oficina.Infraestrutura.Mensageria.Saga;

/// <summary>
/// Trilha da saga no PostgreSQL: só adiciona o passo ao DbContext do consumo; ele é gravado junto com o
/// estado da saga e as mensagens do outbox, na mesma transação.
/// </summary>
public sealed class RegistradorPassoSaga : IRegistradorPassoSaga
{
    private readonly OficinaDbContext _db;

    public RegistradorPassoSaga(OficinaDbContext db) => _db = db;

    public void Registrar(EstadoSagaOrdemServico saga, string passo, string resultado, bool compensacao, string? detalhe = null)
    {
        var agora = DateTime.UtcNow;
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
}
