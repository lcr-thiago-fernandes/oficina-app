using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Oficina.Dominio.OrdensServico;
using Oficina.Infraestrutura.Persistencia;
using Oficina.Infraestrutura.Persistencia.DataSources;
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

        // OrderBy explícito: sem ele, o EF Core só ordena pela chave da OS (única linha
        // aqui), não pela do histórico — a ordem das duas linhas de historico_status
        // fica a critério do plano de execução do Postgres (Id é Guid, sem relação com
        // a ordem cronológica), tornando o `.Last()` abaixo não-determinístico.
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

        // Minor 5 (rodada de correcao 1): sem esta asserção, o teste passaria mesmo que
        // o histórico nunca tivesse sido inserido — "0 depois de apagar" não prova cascata
        // nenhuma se já era 0 antes.
        var antesDeApagar = await _db.Database
            .SqlQuery<int>($"SELECT COUNT(*)::int AS \"Value\" FROM os.historico_status WHERE ordem_servico_id = {os.Id}")
            .SingleAsync();
        antesDeApagar.Should().Be(1);

        _db.OrdensServico.Remove(os);
        await _db.SaveChangesAsync();

        var restantes = await _db.Database
            .SqlQuery<int>($"SELECT COUNT(*)::int AS \"Value\" FROM os.historico_status")
            .SingleAsync();
        restantes.Should().Be(0);
    }

    /// <summary>
    /// Regressão do Achado 1 (rodada de correção 1): a correção de change-tracking não pode
    /// depender de "a navegação Historico não foi carregada" — este teste carrega
    /// deliberadamente a OS com Historico já incluído (via ObterPorIdAsync, que agora inclui
    /// Historico para permitir o cálculo de DuracaoSegundos — ver Achado 4) e só então aplica
    /// uma nova transição. Se a regra voltar a se basear em CollectionEntry.IsLoaded, este
    /// teste falha com DbUpdateConcurrencyException.
    /// </summary>
    [Fact]
    public async Task Nova_transicao_e_salva_mesmo_com_historico_ja_carregado_via_include()
    {
        var dataSource = new OrdemDeServicoDataSource(_db);
        var os = OrdemDeServico.Criar(Guid.NewGuid(), Guid.NewGuid());

        await dataSource.AdicionarAsync(os, CancellationToken.None);
        await dataSource.SalvarAsync(CancellationToken.None);
        _db.ChangeTracker.Clear();

        // ObterPorIdAsync inclui Historico (Achado 4) — a navegação chega IsLoaded == true,
        // com a entrada "Recebida" já carregada do banco.
        var carregada = await dataSource.ObterPorIdAsync(os.Id, CancellationToken.None);
        carregada!.Historico.Should().HaveCount(1);

        carregada.IniciarDiagnostico();
        await dataSource.SalvarAsync(CancellationToken.None); // nao pode lancar DbUpdateConcurrencyException
        _db.ChangeTracker.Clear();

        var recarregada = await dataSource.ObterPorIdAsync(os.Id, CancellationToken.None);
        recarregada!.Historico.Should().HaveCount(2);
        recarregada.Historico.Last().StatusNovo.Should().Be(StatusOrdemDeServico.EmDiagnostico);
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _pg.DisposeAsync();
    }
}
