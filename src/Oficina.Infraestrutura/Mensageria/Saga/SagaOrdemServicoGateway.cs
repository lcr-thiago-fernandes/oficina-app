using MassTransit;
using Microsoft.EntityFrameworkCore;
using Oficina.Aplicacao.OrdensServico.Saga;
using Oficina.Aplicacao.OrdensServico.Saga.Dtos;
using Oficina.Infraestrutura.Mensageria.Internos;
using Oficina.Infraestrutura.Mensageria.Observabilidade;
using Oficina.Infraestrutura.Persistencia;

namespace Oficina.Infraestrutura.Mensageria.Saga;

/// <summary>
/// Inicia/cancela a saga publicando no <see cref="IPublishEndpoint"/> do escopo, que com o bus outbox só
/// adiciona a mensagem ao DbContext: ela é gravada no <c>SalvarAsync</c> seguinte do caso de uso, na mesma
/// transação da OS.
/// </summary>
public sealed class SagaOrdemServicoGateway : ISagaOrdemServicoGateway
{
    public const string HeaderCorrelacao = "X-Correlation-Id";

    private readonly IPublishEndpoint _publish;
    private readonly OficinaDbContext _db;
    private readonly IAcessorCorrelacao _correlacao;

    public SagaOrdemServicoGateway(IPublishEndpoint publish, OficinaDbContext db, IAcessorCorrelacao correlacao)
    {
        _publish = publish;
        _db = db;
        _correlacao = correlacao;
    }

    public Task IniciarAsync(Guid ordemServicoId, CancellationToken ct) =>
        PublicarAsync(new OrdemServicoAberta { OrdemServicoId = ordemServicoId, OcorridoEm = DateTime.UtcNow }, ct);

    public Task SolicitarCancelamentoAsync(Guid ordemServicoId, string motivo, CancellationToken ct) =>
        PublicarAsync(new CancelamentoSolicitado { OrdemServicoId = ordemServicoId, OcorridoEm = DateTime.UtcNow, Motivo = motivo }, ct);

    public async Task<EstadoSagaResponse?> ObterEstadoAsync(Guid ordemServicoId, CancellationToken ct)
    {
        var saga = await _db.Set<EstadoSagaOrdemServico>().AsNoTracking()
            .SingleOrDefaultAsync(s => s.CorrelationId == ordemServicoId, ct);
        if (saga is null)
            return null;

        var trilha = await _db.Set<PassoSaga>().AsNoTracking()
            .Where(p => p.OrdemServicoId == ordemServicoId)
            .OrderBy(p => p.OcorridoEm)
            .Select(p => new PassoSagaResponse(
                p.Passo, p.Resultado, p.Compensacao, new DateTimeOffset(p.OcorridoEm, TimeSpan.Zero), p.Detalhe, p.Estado))
            .ToListAsync(ct);

        return new EstadoSagaResponse(
            saga.CorrelationId,
            saga.NumeroOs,
            saga.EstadoAtual,
            saga.Motivo,
            saga.PagamentoId,
            new DateTimeOffset(saga.CriadaEm, TimeSpan.Zero),
            new DateTimeOffset(saga.AtualizadaEm, TimeSpan.Zero),
            trilha);
    }

    private Task PublicarAsync<T>(T mensagem, CancellationToken ct) where T : class
    {
        var correlacao = _correlacao.Atual;
        return _publish.Publish(mensagem, ctx =>
        {
            if (correlacao is not null)
                ctx.Headers.Set(HeaderCorrelacao, correlacao);
        }, ct);
    }
}
