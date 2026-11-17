using FluentAssertions;
using Moq;
using Oficina.Aplicacao.OrdensServico.Saga.Telemetria;
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

        new RegistradorPassoSaga(db, Mock.Of<IPublicadorEventoSaga>()).Registrar(saga, PassosSaga.GerarOrcamento, PassosSaga.Falha, true, "sem itens");

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

    [Fact]
    public void Registrar_PublicaEventoComDuracaoDesdeUltimoPasso()
    {
        using var db = NovoContexto();
        var publicador = new Mock<IPublicadorEventoSaga>();
        var anterior = DateTime.UtcNow.AddSeconds(-2);
        var saga = new EstadoSagaOrdemServico
        {
            CorrelationId = Guid.NewGuid(), NumeroOs = 77, EstadoAtual = "AguardandoAprovacao", AtualizadaEm = anterior,
        };

        new RegistradorPassoSaga(db, publicador.Object).Registrar(saga, PassosSaga.GerarOrcamento, PassosSaga.Sucesso, false);

        var evento = publicador.Invocations.Should().ContainSingle().Subject.Arguments[0]
            .Should().BeOfType<EventoSagaOrdemServico>().Subject;
        evento.OrdemServicoId.Should().Be(saga.CorrelationId);
        evento.NumeroOs.Should().Be(77);
        evento.Passo.Should().Be(PassosSaga.GerarOrcamento);
        evento.Resultado.Should().Be(PassosSaga.Sucesso);
        evento.Compensacao.Should().BeFalse();
        evento.Estado.Should().Be("AguardandoAprovacao");
        evento.Motivo.Should().BeNull();
        evento.DuracaoMs.Should().BeInRange(1900, 5000);
        saga.AtualizadaEm.Should().BeAfter(anterior);
        db.ChangeTracker.Entries<PassoSaga>().Should().ContainSingle().Which.State.Should().Be(EntityState.Added);
    }

    [Fact]
    public void Registrar_EstornoFalhou_PublicaResultadoCompensacaoFalhou()
    {
        using var db = NovoContexto();
        var publicador = new Mock<IPublicadorEventoSaga>();
        var saga = new EstadoSagaOrdemServico
        {
            CorrelationId = Guid.NewGuid(), EstadoAtual = "Compensando", Motivo = "Estorno falhou: gateway fora",
            AtualizadaEm = DateTime.UtcNow.AddSeconds(-1),
        };

        new RegistradorPassoSaga(db, publicador.Object)
            .Registrar(saga, PassosSaga.EstornarPagamento, PassosSaga.CompensacaoFalhou, true, "gateway fora");

        publicador.Verify(p => p.Publicar(It.Is<EventoSagaOrdemServico>(e =>
            e.Passo == PassosSaga.EstornarPagamento && e.Resultado == "CompensacaoFalhou" && e.Compensacao
            && e.Estado == "Compensando" && e.Motivo == "Estorno falhou: gateway fora" && e.Detalhe == "gateway fora")), Times.Once);
    }

    [Fact]
    public void Registrar_CompensacaoSemDetalhe_PublicaMotivoDaSaga_ESucessoNaoTemMotivo()
    {
        using var db = NovoContexto();
        var publicador = new Mock<IPublicadorEventoSaga>();
        var saga = new EstadoSagaOrdemServico
        {
            CorrelationId = Guid.NewGuid(), EstadoAtual = "Cancelada", Motivo = "cartao_recusado", AtualizadaEm = DateTime.UtcNow,
        };
        var registrador = new RegistradorPassoSaga(db, publicador.Object);

        registrador.Registrar(saga, PassosSaga.ConfirmarPagamento, PassosSaga.Falha, true);
        registrador.Registrar(saga, PassosSaga.Execucao, PassosSaga.Sucesso, false, "EmReparo");

        publicador.Verify(p => p.Publicar(It.Is<EventoSagaOrdemServico>(e =>
            e.Passo == PassosSaga.ConfirmarPagamento && e.Motivo == "cartao_recusado" && e.Detalhe == null)), Times.Once);
        publicador.Verify(p => p.Publicar(It.Is<EventoSagaOrdemServico>(e =>
            e.Passo == PassosSaga.Execucao && e.Motivo == null && e.Detalhe == "EmReparo")), Times.Once);
    }

    [Fact]
    public void Registrar_MotivoExplicito_PrevaleceSobreOMotivoDaSaga_ComDetalheSeparado()
    {
        using var db = NovoContexto();
        var publicador = new Mock<IPublicadorEventoSaga>();
        var saga = new EstadoSagaOrdemServico
        {
            CorrelationId = Guid.NewGuid(), EstadoAtual = "Compensando", Motivo = "antigo", AtualizadaEm = DateTime.UtcNow,
        };

        new RegistradorPassoSaga(db, publicador.Object)
            .Registrar(saga, PassosSaga.EstornarPagamento, PassosSaga.Falha, true, "pg-1", "Pagamento tardio");

        publicador.Verify(p => p.Publicar(It.Is<EventoSagaOrdemServico>(e => e.Motivo == "Pagamento tardio" && e.Detalhe == "pg-1")), Times.Once);
    }
}
