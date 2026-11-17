using FluentAssertions;
using Oficina.Dominio.Estoque;
using Xunit;

namespace Oficina.Dominio.Testes.Estoque;

public class ReservaEstoqueTestes
{
    private static Peca PecaComDez()
    {
        var p = Peca.Criar(Sku.Criar("ABC-123"), "Filtro de óleo", 50m);
        p.RegistrarEntrada(10, "compra");
        return p;
    }

    [Fact]
    public void Reservar_ComSaldo_ReservaSemBaixar()
    {
        var p = PecaComDez();
        var os = Guid.NewGuid();

        var mov = p.Reservar(3, os);

        p.SaldoAtual.Should().Be(10);
        p.SaldoReservado.Should().Be(3);
        p.SaldoDisponivel.Should().Be(7);
        p.SituacaoReserva(os).Should().Be(SituacaoReservaPeca.Reservada);
        mov.Tipo.Should().Be(TipoMovimentacao.Reserva);
        mov.Quantidade.Should().Be(3);
        mov.OrdemServicoId.Should().Be(os);
    }

    [Fact]
    public void Reservar_AlemDoDisponivel_LancaSaldoInsuficiente()
    {
        var p = PecaComDez();
        p.Reservar(8, Guid.NewGuid());

        var act = () => p.Reservar(3, Guid.NewGuid());

        act.Should().Throw<SaldoInsuficienteException>();
        p.SaldoReservado.Should().Be(8);
    }

    [Fact]
    public void Reservar_DuasVezesParaMesmaOs_LancaReservaInvalida()
    {
        var p = PecaComDez();
        var os = Guid.NewGuid();
        p.Reservar(2, os);

        var act = () => p.Reservar(2, os);

        act.Should().Throw<ReservaInvalidaException>();
        p.SaldoReservado.Should().Be(2);
    }

    [Fact]
    public void ConfirmarReserva_BaixaSaldoEZeraReservado()
    {
        var p = PecaComDez();
        var os = Guid.NewGuid();
        p.Reservar(3, os);

        var mov = p.ConfirmarReserva(os);

        p.SaldoAtual.Should().Be(7);
        p.SaldoReservado.Should().Be(0);
        mov.Should().NotBeNull();
        var ultimo = p.Movimentacoes.Last();
        ultimo.Tipo.Should().Be(TipoMovimentacao.Saida);
        ultimo.OrdemServicoId.Should().Be(os);
        ultimo.Quantidade.Should().Be(3);
        ultimo.Motivo.Should().Be("Baixa da reserva da OS");
        p.SituacaoReserva(os).Should().Be(SituacaoReservaPeca.Confirmada);
    }

    [Fact]
    public void ConfirmarReserva_Repetida_RetornaNull()
    {
        var p = PecaComDez();
        var os = Guid.NewGuid();
        p.Reservar(3, os);
        p.ConfirmarReserva(os);

        var segunda = p.ConfirmarReserva(os);

        segunda.Should().BeNull();
        p.SaldoAtual.Should().Be(7);
        p.SaldoReservado.Should().Be(0);
    }

    [Fact]
    public void ConfirmarReserva_SemReserva_LancaReservaInvalida()
    {
        var p = PecaComDez();
        var act = () => p.ConfirmarReserva(Guid.NewGuid());
        act.Should().Throw<ReservaInvalidaException>();
    }

    [Fact]
    public void LiberarReserva_DevolveDisponivel()
    {
        var p = PecaComDez();
        var os = Guid.NewGuid();
        p.Reservar(3, os);

        var mov = p.LiberarReserva(os);

        mov.Should().NotBeNull();
        mov!.Tipo.Should().Be(TipoMovimentacao.LiberacaoReserva);
        p.SaldoAtual.Should().Be(10);
        p.SaldoReservado.Should().Be(0);
        p.SituacaoReserva(os).Should().Be(SituacaoReservaPeca.Liberada);
    }

    [Fact]
    public void LiberarReserva_Repetida_RetornaNull()
    {
        var p = PecaComDez();
        var os = Guid.NewGuid();
        p.Reservar(3, os);
        p.LiberarReserva(os);

        p.LiberarReserva(os).Should().BeNull();
        p.SaldoReservado.Should().Be(0);
    }

    [Fact]
    public void LiberarReserva_SemReserva_RetornaNull()
    {
        var p = PecaComDez();

        p.LiberarReserva(Guid.NewGuid()).Should().BeNull();
        p.SaldoReservado.Should().Be(0);
        p.Movimentacoes.Should().HaveCount(1);
    }

    [Fact]
    public void LiberarReserva_AposConfirmar_LancaReservaInvalida()
    {
        var p = PecaComDez();
        var os = Guid.NewGuid();
        p.Reservar(3, os);
        p.ConfirmarReserva(os);

        var act = () => p.LiberarReserva(os);

        act.Should().Throw<ReservaInvalidaException>();
        p.SaldoAtual.Should().Be(7);
    }

    [Fact]
    public void Repor_AposConfirmar_DevolveSaldo()
    {
        var p = PecaComDez();
        var os = Guid.NewGuid();
        p.Reservar(3, os);
        p.ConfirmarReserva(os);

        var mov = p.Repor(os);

        mov.Should().NotBeNull();
        mov!.Tipo.Should().Be(TipoMovimentacao.Reposicao);
        mov.Quantidade.Should().Be(3);
        p.SaldoAtual.Should().Be(10);
        p.SaldoReservado.Should().Be(0);
        p.SituacaoReserva(os).Should().Be(SituacaoReservaPeca.Reposta);
    }

    [Fact]
    public void Repor_Repetido_RetornaNull()
    {
        var p = PecaComDez();
        var os = Guid.NewGuid();
        p.Reservar(3, os);
        p.ConfirmarReserva(os);
        p.Repor(os);

        p.Repor(os).Should().BeNull();
        p.SaldoAtual.Should().Be(10);
    }

    [Fact]
    public void Repor_SemConfirmacao_LancaReservaInvalida()
    {
        var p = PecaComDez();
        var os = Guid.NewGuid();
        p.Reservar(3, os);

        var act = () => p.Repor(os);

        act.Should().Throw<ReservaInvalidaException>();
        p.SaldoAtual.Should().Be(10);
        p.SaldoReservado.Should().Be(3);
    }

    [Fact]
    public void Reservar_AposLiberar_LancaReservaInvalida()
    {
        var p = PecaComDez();
        var os = Guid.NewGuid();
        p.Reservar(3, os);
        p.LiberarReserva(os);

        var act = () => p.Reservar(3, os);

        act.Should().Throw<ReservaInvalidaException>();
    }

    [Fact]
    public void SituacaoReserva_SemMovimentoDaOs_Nenhuma()
    {
        var p = PecaComDez();
        p.Reservar(3, Guid.NewGuid());

        p.SituacaoReserva(Guid.NewGuid()).Should().Be(SituacaoReservaPeca.Nenhuma);
    }

    [Fact]
    public void RegistrarSaida_ManualComOsQueTemReserva_LancaReservaInvalida()
    {
        // Sem a guarda, a saída manual viraria a "confirmação" da OS sem baixar o reservado,
        // e as unidades ficariam presas em SaldoReservado para sempre.
        var p = PecaComDez();
        var os = Guid.NewGuid();
        p.Reservar(3, os);

        var act = () => p.RegistrarSaida(1, "manual", os);

        act.Should().Throw<ReservaInvalidaException>();
        p.SituacaoReserva(os).Should().Be(SituacaoReservaPeca.Reservada);
        p.SaldoAtual.Should().Be(10);
    }

    [Fact]
    public void RegistrarSaida_AlemDoDisponivel_Lanca()
    {
        var p = PecaComDez();
        p.Reservar(8, Guid.NewGuid());

        var act = () => p.RegistrarSaida(3, "venda balcão", null);

        act.Should().Throw<SaldoInsuficienteException>();
        p.SaldoAtual.Should().Be(10);
        p.SaldoReservado.Should().Be(8);
    }
}
