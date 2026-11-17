using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Oficina.Dominio.OrdensServico;
using Oficina.Infraestrutura.Persistencia;
using Testcontainers.PostgreSql;
using Xunit;

namespace Oficina.Infraestrutura.Testes.Persistencia;

/// <summary>
/// Fase 4: migration <c>Fase4CicloDeVidaOs</c> (EmExecucao → EmReparo, colunas de cancelamento)
/// e concorrência otimista por <c>xmin</c>.
/// </summary>
public class CicloDeVidaOsPersistenciaTestes : IAsyncLifetime
{
    private const string MigrationAnterior = "20260914125322_HistoricoStatusEUnidade";

    private readonly PostgreSqlContainer _pg = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .Build();

    public Task InitializeAsync() => _pg.StartAsync();

    private OficinaDbContext NovoContexto() => new(
        new DbContextOptionsBuilder<OficinaDbContext>().UseNpgsql(_pg.GetConnectionString()).Options);

    [Fact]
    public async Task Migration_RenomeiaEmExecucaoParaEmReparo_EDownDesfaz()
    {
        await using var db = NovoContexto();
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync(MigrationAnterior);

        var osId = Guid.NewGuid();
        await db.Database.ExecuteSqlInterpolatedAsync($@"
            INSERT INTO os.ordem_servico (id, cliente_id, veiculo_id, status, criada_em)
            VALUES ({osId}, {Guid.NewGuid()}, {Guid.NewGuid()}, 'EmExecucao', now());");
        await db.Database.ExecuteSqlInterpolatedAsync($@"
            INSERT INTO os.historico_status (id, ordem_servico_id, status_anterior, status_novo, ocorrido_em)
            VALUES ({Guid.NewGuid()}, {osId}, 'AguardandoAprovacao', 'EmExecucao', now()),
                   ({Guid.NewGuid()}, {osId}, 'EmExecucao', 'Finalizada', now());");

        await migrator.MigrateAsync();

        (await Escalar(db, $"SELECT count(*)::int AS \"Value\" FROM os.ordem_servico WHERE status = 'EmReparo'")).Should().Be(1);
        (await Escalar(db, $"SELECT count(*)::int AS \"Value\" FROM os.historico_status WHERE status_novo = 'EmReparo'")).Should().Be(1);
        (await Escalar(db, $"SELECT count(*)::int AS \"Value\" FROM os.historico_status WHERE status_anterior = 'EmReparo'")).Should().Be(1);
        (await Escalar(db, $"SELECT count(*)::int AS \"Value\" FROM os.historico_status WHERE status_novo = 'EmExecucao' OR status_anterior = 'EmExecucao'")).Should().Be(0);

        var carregada = await db.OrdensServico.AsNoTracking().SingleAsync(o => o.Id == osId);
        carregada.Status.Should().Be(StatusOrdemDeServico.EmReparo);

        await migrator.MigrateAsync(MigrationAnterior);

        (await Escalar(db, $"SELECT count(*)::int AS \"Value\" FROM os.ordem_servico WHERE status = 'EmExecucao'")).Should().Be(1);
        (await Escalar(db, $"SELECT count(*)::int AS \"Value\" FROM os.historico_status WHERE status_novo = 'EmExecucao' OR status_anterior = 'EmExecucao'")).Should().Be(2);
    }

    [Fact]
    public async Task Cancelamento_PersisteMotivoEData()
    {
        await using (var db = NovoContexto()) await db.Database.MigrateAsync();

        var os = OrdemDeServico.Criar(Guid.NewGuid(), Guid.NewGuid());
        os.Cancelar("Cliente desistiu");
        await using (var db = NovoContexto())
        {
            db.OrdensServico.Add(os);
            await db.SaveChangesAsync();
        }

        await using (var db = NovoContexto())
        {
            var carregada = await db.OrdensServico.SingleAsync(o => o.Id == os.Id);
            carregada.Status.Should().Be(StatusOrdemDeServico.Cancelada);
            carregada.MotivoCancelamento.Should().Be("Cliente desistiu");
            carregada.CanceladaEm.Should().BeCloseTo(os.CanceladaEm!.Value, TimeSpan.FromMilliseconds(1));
        }
    }

    [Fact]
    public async Task AtualizacoesConcorrentes_NaMesmaOs_SegundaLancaConcorrencia()
    {
        await using (var db = NovoContexto()) await db.Database.MigrateAsync();

        var os = OrdemDeServico.Criar(Guid.NewGuid(), Guid.NewGuid());
        os.AdicionarItemServico(Guid.NewGuid(), "S", 10m, 1);
        await using (var db = NovoContexto())
        {
            db.OrdensServico.Add(os);
            await db.SaveChangesAsync();
        }

        await using var atendente = NovoContexto();
        await using var saga = NovoContexto();
        var pelaSaga = await saga.OrdensServico.Include(o => o.ItensServico).Include(o => o.Historico).SingleAsync(o => o.Id == os.Id);
        var peloAtendente = await atendente.OrdensServico.Include(o => o.ItensServico).Include(o => o.Historico).SingleAsync(o => o.Id == os.Id);

        pelaSaga.AguardarAprovacao();
        await saga.SaveChangesAsync();

        peloAtendente.Cancelar("motivo");
        var act = () => atendente.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }

    [Fact]
    public async Task CoordenadaPorSaga_LinhasExistentesFicamFalse_ENovaMarcadaPersisteTrue()
    {
        await using var db = NovoContexto();
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20261117022506_Fase4ReservaEstoque");

        // OS da Fase 3, gravada antes da coluna existir.
        var legadaId = Guid.NewGuid();
        await db.Database.ExecuteSqlInterpolatedAsync($@"
            INSERT INTO os.ordem_servico (id, cliente_id, veiculo_id, status, criada_em)
            VALUES ({legadaId}, {Guid.NewGuid()}, {Guid.NewGuid()}, 'Recebida', now());");

        await migrator.MigrateAsync();

        (await Escalar(db, $@"SELECT count(*)::int AS ""Value"" FROM information_schema.columns
            WHERE table_schema = 'os' AND table_name = 'ordem_servico' AND column_name = 'coordenada_por_saga'
              AND is_nullable = 'NO' AND data_type = 'boolean' AND column_default = 'false'")).Should().Be(1);
        (await db.OrdensServico.AsNoTracking().SingleAsync(o => o.Id == legadaId)).CoordenadaPorSaga.Should().BeFalse();

        var nova = OrdemDeServico.Criar(Guid.NewGuid(), Guid.NewGuid());
        nova.MarcarCoordenadaPorSaga();
        await using (var escrita = NovoContexto())
        {
            escrita.OrdensServico.Add(nova);
            await escrita.SaveChangesAsync();
        }

        await using var leitura = NovoContexto();
        (await leitura.OrdensServico.SingleAsync(o => o.Id == nova.Id)).CoordenadaPorSaga.Should().BeTrue();
    }

    private static Task<int> Escalar(OficinaDbContext db, FormattableString sql) =>
        db.Database.SqlQuery<int>(sql).SingleAsync();

    public Task DisposeAsync() => _pg.DisposeAsync().AsTask();
}
