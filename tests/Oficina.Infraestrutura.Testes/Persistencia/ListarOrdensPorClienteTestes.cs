using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Oficina.Dominio.OrdensServico;
using Oficina.Infraestrutura.Persistencia;
using Oficina.Infraestrutura.Persistencia.DataSources;
using Testcontainers.PostgreSql;
using Xunit;

namespace Oficina.Infraestrutura.Testes.Persistencia;

public class ListarOrdensPorClienteTestes : IAsyncLifetime
{
    private readonly PostgreSqlContainer _pg = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .Build();

    private OficinaDbContext _db = null!;
    private OrdemDeServicoDataSource _ds = null!;

    public async Task InitializeAsync()
    {
        await _pg.StartAsync();
        var options = new DbContextOptionsBuilder<OficinaDbContext>()
            .UseNpgsql(_pg.GetConnectionString())
            .Options;
        _db = new OficinaDbContext(options);
        await _db.Database.MigrateAsync();
        _ds = new OrdemDeServicoDataSource(_db);
    }

    [Fact]
    public async Task Lista_apenas_as_ordens_do_cliente_informado()
    {
        var clienteA = Guid.NewGuid();
        var clienteB = Guid.NewGuid();

        _db.OrdensServico.Add(OrdemDeServico.Criar(clienteA, Guid.NewGuid()));
        _db.OrdensServico.Add(OrdemDeServico.Criar(clienteA, Guid.NewGuid()));
        _db.OrdensServico.Add(OrdemDeServico.Criar(clienteB, Guid.NewGuid()));
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var doA = await _ds.ListarPorClienteAsync(clienteA, CancellationToken.None);

        doA.Should().HaveCount(2);
        doA.Should().OnlyContain(o => o.ClienteId == clienteA);
    }

    [Fact]
    public async Task Retorna_lista_vazia_quando_o_cliente_nao_tem_ordens()
    {
        var resultado = await _ds.ListarPorClienteAsync(Guid.NewGuid(), CancellationToken.None);
        resultado.Should().BeEmpty();
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _pg.DisposeAsync();
    }
}
