using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using Oficina.Dominio.Estoque;
using Oficina.Infraestrutura.Mensageria.Saga;

namespace Oficina.Integracao.Testes.Saga;

/// <summary>
/// Interceptador do <c>SaveChanges</c> registrado na Api dos testes. Inerte até um teste armar uma falha:
/// <list type="bullet">
/// <item>conflito de <c>xmin</c>: antes da próxima gravação que altera uma <see cref="Peca"/>, outra conexão
/// atualiza a mesma linha (o <c>xmin</c> muda e o UPDATE do contexto perde a disputa);</item>
/// <item>falha do outbox: toda gravação de mensagem do outbox no consumo da saga da OS alvo lança.</item>
/// </list>
/// </summary>
public sealed class InjetorDeFalhasNoBanco : SaveChangesInterceptor
{
    private readonly string _conexao;
    private int _conflitoArmado;
    private int _conflitosInjetados;
    private Guid? _falharOutboxDaOs;
    private int _falhasDeOutbox;

    public InjetorDeFalhasNoBanco(string conexao) => _conexao = conexao;

    public int ConflitosInjetados => Volatile.Read(ref _conflitosInjetados);
    public int FalhasDeOutbox => Volatile.Read(ref _falhasDeOutbox);

    public void ArmarConflitoNaProximaGravacaoDePeca() => Interlocked.Exchange(ref _conflitoArmado, 1);

    public void FalharOutboxDaSaga(Guid? ordemServicoId) => _falharOutboxDaOs = ordemServicoId;

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        var contexto = eventData.Context!;

        if (Volatile.Read(ref _conflitoArmado) == 1)
        {
            var peca = contexto.ChangeTracker.Entries<Peca>().FirstOrDefault(e => e.State == EntityState.Modified);
            if (peca is not null && Interlocked.Exchange(ref _conflitoArmado, 0) == 1)
            {
                await using var conexao = new NpgsqlConnection(_conexao);
                await conexao.OpenAsync(cancellationToken);
                await using var comando = new NpgsqlCommand(
                    "UPDATE estoque.peca SET saldo_atual = saldo_atual WHERE id = @id", conexao);
                comando.Parameters.AddWithValue("id", peca.Entity.Id);
                await comando.ExecuteNonQueryAsync(cancellationToken);
                Interlocked.Increment(ref _conflitosInjetados);
            }
        }

        if (_falharOutboxDaOs is { } alvo
            && contexto.ChangeTracker.Entries<MassTransit.EntityFrameworkCoreIntegration.OutboxMessage>().Any(e => e.State == EntityState.Added)
            && contexto.ChangeTracker.Entries<EstadoSagaOrdemServico>().Any(e => e.Entity.CorrelationId == alvo))
        {
            Interlocked.Increment(ref _falhasDeOutbox);
            throw new InvalidOperationException("Falha simulada ao gravar o outbox da saga.");
        }

        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}
