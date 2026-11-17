using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Oficina.Dominio.Estoque;
using Oficina.Infraestrutura.Persistencia;
using Testcontainers.PostgreSql;
using Xunit;

namespace Oficina.Infraestrutura.Testes.Persistencia;

/// <summary>
/// Fase 4: reserva de estoque (migration <c>Fase4ReservaEstoque</c>): coluna <c>saldo_reservado</c>,
/// novos códigos de movimento e concorrência otimista por <c>xmin</c> na peça.
/// </summary>
public class ReservaEstoquePersistenciaTestes : IAsyncLifetime
{
    private readonly PostgreSqlContainer _pg = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .Build();

    public async Task InitializeAsync()
    {
        await _pg.StartAsync();
        await using var db = NovoContexto();
        await db.Database.MigrateAsync();
    }

    private OficinaDbContext NovoContexto() => new(
        new DbContextOptionsBuilder<OficinaDbContext>().UseNpgsql(_pg.GetConnectionString()).Options);

    private async Task<Peca> PecaGravadaComDez()
    {
        var peca = Peca.Criar(Sku.Criar($"SKU-{Guid.NewGuid():N}"[..20]), "Filtro", 50m);
        peca.RegistrarEntrada(10, "compra");
        await using var db = NovoContexto();
        db.Pecas.Add(peca);
        await db.SaveChangesAsync();
        return peca;
    }

    private static Task<Peca> Carregar(OficinaDbContext db, Guid id) =>
        db.Pecas.Include(p => p.Movimentacoes).SingleAsync(p => p.Id == id);

    [Fact]
    public async Task ReservarConfirmarRepor_PersisteSaldosETiposDeMovimento()
    {
        var peca = await PecaGravadaComDez();
        var os = Guid.NewGuid();

        await using (var db = NovoContexto())
        {
            var p = await Carregar(db, peca.Id);
            db.Add(p.Reservar(3, os));
            await db.SaveChangesAsync();
        }

        await using (var db = NovoContexto())
        {
            var p = await Carregar(db, peca.Id);
            p.SaldoReservado.Should().Be(3);
            p.SaldoAtual.Should().Be(10);
            p.SaldoDisponivel.Should().Be(7);
            db.Add(p.ConfirmarReserva(os)!);
            await db.SaveChangesAsync();
        }

        await using (var db = NovoContexto())
        {
            var p = await Carregar(db, peca.Id);
            p.SaldoReservado.Should().Be(0);
            p.SaldoAtual.Should().Be(7);
            db.Add(p.Repor(os)!);
            await db.SaveChangesAsync();
        }

        await using (var db = NovoContexto())
        {
            var p = await Carregar(db, peca.Id);
            p.SaldoAtual.Should().Be(10);
            p.SaldoReservado.Should().Be(0);
            p.SituacaoReserva(os).Should().Be(SituacaoReservaPeca.Reposta);

            var codigos = await db.Database
                .SqlQuery<string>($"SELECT tipo AS \"Value\" FROM estoque.movimentacao WHERE ordem_servico_id = {os} ORDER BY criado_em")
                .ToListAsync();
            codigos.Should().Equal("R", "S", "P");
            p.Movimentacoes.Where(m => m.OrdemServicoId == os).OrderBy(m => m.CriadoEm).Select(m => m.Tipo)
                .Should().Equal(TipoMovimentacao.Reserva, TipoMovimentacao.Saida, TipoMovimentacao.Reposicao);
        }
    }

    [Fact]
    public async Task LiberarReserva_PersisteCodigoL()
    {
        var peca = await PecaGravadaComDez();
        var os = Guid.NewGuid();

        await using (var db = NovoContexto())
        {
            var p = await Carregar(db, peca.Id);
            db.Add(p.Reservar(4, os));
            db.Add(p.LiberarReserva(os)!);
            await db.SaveChangesAsync();
        }

        await using (var db = NovoContexto())
        {
            var p = await Carregar(db, peca.Id);
            p.SaldoReservado.Should().Be(0);
            p.SituacaoReserva(os).Should().Be(SituacaoReservaPeca.Liberada);
            var codigo = await db.Database
                .SqlQuery<string>($"SELECT tipo AS \"Value\" FROM estoque.movimentacao WHERE ordem_servico_id = {os} AND tipo = 'L'")
                .SingleAsync();
            codigo.Should().Be("L");
        }
    }

    [Fact]
    public async Task SituacaoReserva_AposRecarga_IndependeDaOrdemDeCarga()
    {
        var peca = await PecaGravadaComDez();
        var os = Guid.NewGuid();

        await using (var db = NovoContexto())
        {
            var p = await Carregar(db, peca.Id);
            db.Add(p.Reservar(3, os));
            db.Add(p.ConfirmarReserva(os)!);
            await db.SaveChangesAsync();

            // Mesmo instante (truncado a microssegundos) para R e S: só a precedência de tipo desempata.
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE estoque.movimentacao SET criado_em = date_trunc('second', now()) WHERE ordem_servico_id = {os}");
        }

        await using (var db = NovoContexto())
        {
            var p = await Carregar(db, peca.Id);
            var daOs = p.Movimentacoes.Where(m => m.OrdemServicoId == os).ToList();
            daOs.Should().HaveCount(2);
            daOs.Select(m => m.CriadoEm).Distinct().Should().ContainSingle();

            p.SituacaoReserva(os).Should().Be(SituacaoReservaPeca.Confirmada);
        }
    }

    [Fact]
    public async Task SaldoReservadoAcimaDoSaldoAtual_ViolaCheckConstraint()
    {
        var peca = await PecaGravadaComDez();
        await using var db = NovoContexto();

        var act = () => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE estoque.peca SET saldo_reservado = 11 WHERE id = {peca.Id}");

        (await act.Should().ThrowAsync<Npgsql.PostgresException>())
            .Which.ConstraintName.Should().Be("ck_peca_saldo_reservado_valido");
    }

    [Fact]
    public async Task CodigoDeMovimentoDesconhecido_AoCarregar_LancaInvalidOperation()
    {
        var peca = await PecaGravadaComDez();
        await using var db = NovoContexto();
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE estoque.movimentacao SET tipo = 'X' WHERE peca_id = {peca.Id}");

        var act = () => Carregar(db, peca.Id);

        (await act.Should().ThrowAsync<InvalidOperationException>()).WithMessage("*'X'*");
    }

    [Fact]
    public async Task Concorrencia_DuasInstanciasAlterandoMesmaPeca_LancaDbUpdateConcurrencyException()
    {
        var peca = await PecaGravadaComDez();

        await using var primeiro = NovoContexto();
        await using var segundo = NovoContexto();
        var pelaPrimeira = await Carregar(primeiro, peca.Id);
        var pelaSegunda = await Carregar(segundo, peca.Id);

        primeiro.Add(pelaPrimeira.Reservar(6, Guid.NewGuid()));
        await primeiro.SaveChangesAsync();

        segundo.Add(pelaSegunda.Reservar(6, Guid.NewGuid()));
        var act = () => segundo.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateConcurrencyException>();

        await using var verificacao = NovoContexto();
        (await Carregar(verificacao, peca.Id)).SaldoReservado.Should().Be(6);
    }

    public Task DisposeAsync() => _pg.DisposeAsync().AsTask();
}
