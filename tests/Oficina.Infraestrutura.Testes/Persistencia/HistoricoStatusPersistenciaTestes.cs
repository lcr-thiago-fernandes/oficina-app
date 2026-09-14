using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Oficina.Dominio.OrdensServico;
using Oficina.Infraestrutura.Persistencia;
using Testcontainers.PostgreSql;
using Xunit;

namespace Oficina.Infraestrutura.Testes.Persistencia;

public class HistoricoStatusPersistenciaTestes : IAsyncLifetime
{
    private readonly PostgreSqlContainer _pg = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .Build();

    private OficinaDbContext _db = null!;

    public async Task InitializeAsync()
    {
        await _pg.StartAsync();
        var options = new DbContextOptionsBuilder<OficinaDbContext>()
            .UseNpgsql(_pg.GetConnectionString())
            .Options;
        _db = new OficinaDbContext(options);
        await _db.Database.MigrateAsync();
    }

    [Fact]
    public async Task Historico_e_persistido_e_recarregado_com_a_ordem()
    {
        var os = OrdemDeServico.Criar(Guid.NewGuid(), Guid.NewGuid(), null, "filial-sp");
        os.IniciarDiagnostico();

        _db.OrdensServico.Add(os);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        // OrderBy explicito: sem ele, o EF Core so ordena pela chave da OS (unica linha
        // aqui), nao pela do historico — a ordem das duas linhas de historico_status
        // fica a criterio do plano de execucao do Postgres (Id e Guid, sem relacao com
        // a ordem cronologica), tornando o `.Last()` abaixo nao-deterministico.
        var recarregada = await _db.OrdensServico
            .Include(o => o.Historico.OrderBy(h => h.OcorridoEm))
            .FirstAsync(o => o.Id == os.Id);

        recarregada.Unidade.Should().Be("filial-sp");
        recarregada.Historico.Should().HaveCount(2);
        recarregada.Historico.Last().StatusNovo.Should().Be(StatusOrdemDeServico.EmDiagnostico);
        recarregada.Historico.Last().StatusAnterior.Should().Be(StatusOrdemDeServico.Recebida);
    }

    [Fact]
    public async Task Apagar_a_ordem_apaga_o_historico_em_cascata()
    {
        var os = OrdemDeServico.Criar(Guid.NewGuid(), Guid.NewGuid());
        _db.OrdensServico.Add(os);
        await _db.SaveChangesAsync();

        _db.OrdensServico.Remove(os);
        await _db.SaveChangesAsync();

        var restantes = await _db.Database
            .SqlQuery<int>($"SELECT COUNT(*)::int AS \"Value\" FROM os.historico_status")
            .SingleAsync();
        restantes.Should().Be(0);
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _pg.DisposeAsync();
    }
}
