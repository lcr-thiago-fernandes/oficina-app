using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Oficina.Infraestrutura.Mensageria.Saga;
using Oficina.Infraestrutura.Persistencia;
using Testcontainers.PostgreSql;
using Xunit;

namespace Oficina.Infraestrutura.Testes.Persistencia;

/// <summary>
/// PagamentoId é string sem formato fixo no contrato: ids do Mercado Pago são numéricos e curtos, mas o
/// simulador do Billing gera ids de 72+ caracteres (sim-a-{osId}-{valor}-{guid}). A coluna aceita até 128.
/// </summary>
public class EstadoSagaPersistenciaTestes : IAsyncLifetime
{
    private readonly PostgreSqlContainer _pg = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .Build();

    private OficinaDbContext _db = null!;

    public async Task InitializeAsync()
    {
        await _pg.StartAsync();
        _db = new OficinaDbContext(new DbContextOptionsBuilder<OficinaDbContext>()
            .UseNpgsql(_pg.GetConnectionString())
            .Options);
        await _db.Database.MigrateAsync();
    }

    [Fact]
    public async Task Salvar_PagamentoIdDe128Caracteres_PersisteERecarrega()
    {
        var pagamentoId = "sim-a-" + new string('9', 122);
        var saga = new EstadoSagaOrdemServico
        {
            CorrelationId = Guid.NewGuid(),
            EstadoAtual = "EmExecucao",
            NumeroOs = 1,
            PagamentoId = pagamentoId,
            CriadaEm = DateTime.UtcNow,
            AtualizadaEm = DateTime.UtcNow,
        };

        _db.Set<EstadoSagaOrdemServico>().Add(saga);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var recarregada = await _db.Set<EstadoSagaOrdemServico>().SingleAsync(s => s.CorrelationId == saga.CorrelationId);
        pagamentoId.Should().HaveLength(128);
        recarregada.PagamentoId.Should().Be(pagamentoId);
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _pg.DisposeAsync();
    }
}
