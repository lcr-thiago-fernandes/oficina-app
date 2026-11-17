using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Oficina.Infraestrutura.Mensageria.Saga;
using Oficina.Infraestrutura.Persistencia;

namespace Oficina.Infraestrutura.Testes.Mensageria;

/// <summary>Só o ChangeTracker (sem SaveChanges): a conexão é fictícia.</summary>
public class RegistradorPassoSagaTestes
{
    private static OficinaDbContext NovoContexto() =>
        new(new DbContextOptionsBuilder<OficinaDbContext>()
            .UseNpgsql("Host=127.0.0.1;Port=1;Database=x;Username=x;Password=x").Options);

    [Fact]
    public void Registrar_AdicionaPassoComEstadoAtualDaSagaEAtualizaAtualizadaEm()
    {
        using var db = NovoContexto();
        var saga = new EstadoSagaOrdemServico
        {
            CorrelationId = Guid.NewGuid(),
            EstadoAtual = "Cancelada",
            AtualizadaEm = DateTime.UtcNow.AddMinutes(-5),
        };

        new RegistradorPassoSaga(db).Registrar(saga, PassosSaga.GerarOrcamento, PassosSaga.Falha, true, "sem itens");

        var passo = db.ChangeTracker.Entries<PassoSaga>().Should().ContainSingle().Subject;
        passo.State.Should().Be(EntityState.Added);
        passo.Entity.OrdemServicoId.Should().Be(saga.CorrelationId);
        passo.Entity.Passo.Should().Be(PassosSaga.GerarOrcamento);
        passo.Entity.Resultado.Should().Be(PassosSaga.Falha);
        passo.Entity.Compensacao.Should().BeTrue();
        passo.Entity.Detalhe.Should().Be("sem itens");
        passo.Entity.Estado.Should().Be("Cancelada");
        passo.Entity.Id.Should().NotBeEmpty();
        passo.Entity.OcorridoEm.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        passo.Entity.OcorridoEm.Kind.Should().Be(DateTimeKind.Utc);
        saga.AtualizadaEm.Should().Be(passo.Entity.OcorridoEm);
    }
}
