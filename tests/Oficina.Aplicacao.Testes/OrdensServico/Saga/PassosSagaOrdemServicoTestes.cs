using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Oficina.Aplicacao.Clientes.Gateways;
using Oficina.Aplicacao.Estoque.Gateways;
using Oficina.Aplicacao.OrdensServico.Gateways;
using Oficina.Aplicacao.OrdensServico.Saga;
using Oficina.Aplicacao.OrdensServico.Saga.Dtos;
using Oficina.Aplicacao.OrdensServico.Telemetria;
using Oficina.Dominio.Clientes;
using Oficina.Dominio.Estoque;
using Oficina.Dominio.OrdensServico;
using Oficina.Testes.Compartilhado;
using Xunit;

namespace Oficina.Aplicacao.Testes.OrdensServico.Saga;

public class PassosSagaOrdemServicoTestes
{
    private const string Motivo = "Cliente desistiu";

    private readonly Mock<IOrdemDeServicoGateway> _ordens = new();
    private readonly Mock<IPecaGateway> _pecas = new();
    private readonly Mock<IClienteGateway> _clientes = new();
    private readonly Mock<INotificacaoGateway> _notificacoes = new();
    private readonly Mock<IPublicadorEventoOs> _publicador = new();

    private readonly Cliente _cliente = Cliente.Criar("João", Documento.Criar("39053344705"),
        Email.Criar("joao@x.com"), Telefone.Criar("11987654321"));

    private PassosSagaOrdemServico Sut => new(
        _ordens.Object, _pecas.Object, _clientes.Object, _notificacoes.Object, _publicador.Object,
        NullLogger<PassosSagaOrdemServico>.Instance);

    // Peça "Filtro" com saldo 10 e OS com 3 unidades reservadas (como sai da abertura).
    private (OrdemDeServico Os, Peca Peca) OsComReserva(StatusOrdemDeServico status, params int[] quantidadesPorItem)
    {
        if (quantidadesPorItem.Length == 0) quantidadesPorItem = new[] { 3 };

        var peca = Peca.Criar(Sku.Criar("FIL-001"), "Filtro", 50m);
        peca.RegistrarEntrada(10, "Compra");

        var os = OrdemDeServico.Criar(_cliente.Id, Guid.NewGuid());
        os.AdicionarItemServico(Guid.NewGuid(), "Troca de óleo", 150m, 1);
        foreach (var q in quantidadesPorItem)
            os.AdicionarItemPeca(peca.Id, peca.Nome, peca.PrecoUnitario, q);
        peca.Reservar(quantidadesPorItem.Sum(), os.Id);

        OrdemEmStatus.Avancar(os, status);
        Registrar(os, peca);
        return (os, peca);
    }

    private void Registrar(OrdemDeServico os, params Peca[] pecas)
    {
        _ordens.Setup(o => o.ObterPorIdAsync(os.Id, It.IsAny<CancellationToken>())).ReturnsAsync(os);
        foreach (var p in pecas)
            _pecas.Setup(g => g.ObterPorIdAsync(p.Id, It.IsAny<CancellationToken>())).ReturnsAsync(p);
        _clientes.Setup(c => c.ObterPorIdAsync(_cliente.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_cliente);
    }

    private void VerificarNadaPersistido()
    {
        _ordens.Verify(o => o.SalvarAsync(It.IsAny<CancellationToken>()), Times.Never);
        _pecas.Verify(p => p.MarcarMovimentacaoComoNova(It.IsAny<MovimentacaoEstoque>()), Times.Never);
        _publicador.Verify(p => p.Publicar(It.IsAny<EventoOrdemServico>()), Times.Never);
        _notificacoes.Verify(n => n.NotificarMudancaDeStatusAsync(It.IsAny<OrdemDeServico>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task OsInexistente_LancaInvalidOperation()
    {
        _ordens.Setup(o => o.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((OrdemDeServico?)null);

        var act = async () => await Sut.RegistrarOrcamentoGeradoAsync(Guid.NewGuid(), default);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task MontarPedidoOrcamento_MapeiaClienteEItensComPrecoSnapshot()
    {
        var (os, peca) = OsComReserva(StatusOrdemDeServico.Recebida);

        var pedido = await Sut.MontarPedidoOrcamentoAsync(os.Id, default);

        pedido.NumeroOs.Should().Be(os.Numero);
        pedido.ClienteDocumento.Should().Be(_cliente.Documento.Valor);
        pedido.ClienteNome.Should().Be("João");
        pedido.ClienteEmail.Should().Be(_cliente.Email.Valor);
        pedido.Itens.Should().HaveCount(2);
        var servico = os.ItensServico.Single();
        pedido.Itens[0].Should().Be(new ItemPedidoOrcamento(
            TipoItemPedido.Servico, servico.ServicoId, "Troca de óleo", 1, 150m));
        pedido.Itens[1].Should().Be(new ItemPedidoOrcamento(
            TipoItemPedido.Peca, peca.Id, "Filtro", 3, 50m));
    }

    [Fact]
    public async Task RegistrarOrcamentoGerado_MoveParaAguardandoAprovacao_EPublicaEvento()
    {
        var (os, _) = OsComReserva(StatusOrdemDeServico.Recebida);

        await Sut.RegistrarOrcamentoGeradoAsync(os.Id, default);

        os.Status.Should().Be(StatusOrdemDeServico.AguardandoAprovacao);
        _publicador.Verify(p => p.Publicar(It.Is<EventoOrdemServico>(e =>
            e.StatusAnterior == "Recebida" && e.StatusNovo == "AguardandoAprovacao")), Times.Once);
        _ordens.Verify(o => o.SalvarAsync(It.IsAny<CancellationToken>()), Times.Once);
        _notificacoes.Verify(n => n.NotificarMudancaDeStatusAsync(os, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RegistrarOrcamentoGerado_Duplicado_NaoFazNada()
    {
        var (os, _) = OsComReserva(StatusOrdemDeServico.AguardandoAprovacao);

        await Sut.RegistrarOrcamentoGeradoAsync(os.Id, default);

        os.Status.Should().Be(StatusOrdemDeServico.AguardandoAprovacao);
        VerificarNadaPersistido();
    }

    [Fact]
    public async Task RegistrarOrcamentoGerado_OsCancelada_NaoFazNada()
    {
        var (os, _) = OsComReserva(StatusOrdemDeServico.Cancelada);

        await Sut.RegistrarOrcamentoGeradoAsync(os.Id, default);

        os.Status.Should().Be(StatusOrdemDeServico.Cancelada);
        VerificarNadaPersistido();
    }

    [Fact]
    public async Task RegistrarOrcamentoAprovado_MoveParaAguardandoPagamento()
    {
        var (os, _) = OsComReserva(StatusOrdemDeServico.AguardandoAprovacao);

        await Sut.RegistrarOrcamentoAprovadoAsync(os.Id, default);
        await Sut.RegistrarOrcamentoAprovadoAsync(os.Id, default);

        os.Status.Should().Be(StatusOrdemDeServico.AguardandoPagamento);
        _ordens.Verify(o => o.SalvarAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ConfirmarPagamento_ConfirmaReservaDeCadaPeca_ERetornaPedidoExecucao()
    {
        var (os, peca) = OsComReserva(StatusOrdemDeServico.AguardandoPagamento);

        var pedido = await Sut.ConfirmarPagamentoAsync(os.Id, default);

        peca.SaldoAtual.Should().Be(7);
        peca.SaldoReservado.Should().Be(0);
        os.Status.Should().Be(StatusOrdemDeServico.AguardandoPagamento);
        pedido.Should().NotBeNull();
        pedido!.NumeroOs.Should().Be(os.Numero);
        pedido.Unidade.Should().Be("matriz");
        pedido.Pecas[0].Should().Be(new ItemPedidoExecucao("Filtro", 3));
        pedido.Servicos.Should().ContainSingle().Which.Should().Be(new ItemPedidoExecucao("Troca de óleo", 1));
        _pecas.Verify(p => p.MarcarMovimentacaoComoNova(It.Is<MovimentacaoEstoque>(m =>
            m.Tipo == TipoMovimentacao.Saida && m.Quantidade == 3)), Times.Once);
        _ordens.Verify(o => o.SalvarAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ConfirmarPagamento_Repetido_NaoBaixaDuasVezes()
    {
        var (os, peca) = OsComReserva(StatusOrdemDeServico.AguardandoPagamento);

        var primeiro = await Sut.ConfirmarPagamentoAsync(os.Id, default);
        var segundo = await Sut.ConfirmarPagamentoAsync(os.Id, default);

        peca.SaldoAtual.Should().Be(7);
        segundo.Should().BeEquivalentTo(primeiro);
        _pecas.Verify(p => p.MarcarMovimentacaoComoNova(It.IsAny<MovimentacaoEstoque>()), Times.Once);
    }

    [Fact]
    public async Task ConfirmarPagamento_MesmaPecaEmDoisItens_ConfirmaSomaUmaVez()
    {
        var (os, peca) = OsComReserva(StatusOrdemDeServico.AguardandoPagamento, 1, 2);

        var pedido = await Sut.ConfirmarPagamentoAsync(os.Id, default);

        peca.SaldoAtual.Should().Be(7);
        peca.SaldoReservado.Should().Be(0);
        pedido!.Pecas.Should().HaveCount(2);
        _pecas.Verify(p => p.ObterPorIdAsync(peca.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ConfirmarPagamento_AposReservaLiberada_RetornaNullSemLancarNemBaixar()
    {
        // Redelivery: o pagamento chega depois de a reserva ter sido liberada.
        var (os, peca) = OsComReserva(StatusOrdemDeServico.AguardandoPagamento);
        peca.LiberarReserva(os.Id);

        var pedido = await Sut.ConfirmarPagamentoAsync(os.Id, default);

        pedido.Should().BeNull("a saga decide pelo EstornarPagamento");
        peca.SaldoAtual.Should().Be(10);
        VerificarNadaPersistido();
    }

    [Fact]
    public async Task ConfirmarPagamento_OsCancelada_RetornaNullSemMexerNoEstoque()
    {
        var (os, peca) = OsComReserva(StatusOrdemDeServico.AguardandoPagamento);
        await Sut.CancelarAsync(os.Id, Motivo, default);
        _ordens.Invocations.Clear();
        _pecas.Invocations.Clear();
        _publicador.Invocations.Clear();
        _notificacoes.Invocations.Clear();

        var pedido = await Sut.ConfirmarPagamentoAsync(os.Id, default);

        pedido.Should().BeNull();
        peca.SaldoAtual.Should().Be(10);
        peca.SaldoReservado.Should().Be(0);
        VerificarNadaPersistido();
    }

    [Fact]
    public async Task ConfirmarPagamento_UmaPecaLiberadaOutraReservada_NaoConfirmaNenhuma()
    {
        var (os, filtro) = OsComReserva(StatusOrdemDeServico.Recebida);
        // segunda peça da mesma OS, ainda reservada
        var vela = Peca.Criar(Sku.Criar("VEL-001"), "Vela", 20m);
        vela.RegistrarEntrada(5, "Compra");
        os.AdicionarItemPeca(vela.Id, vela.Nome, 20m, 2);
        vela.Reservar(2, os.Id);
        OrdemEmStatus.Avancar(os, StatusOrdemDeServico.AguardandoPagamento);
        Registrar(os, filtro, vela);
        filtro.LiberarReserva(os.Id);

        var pedido = await Sut.ConfirmarPagamentoAsync(os.Id, default);

        pedido.Should().BeNull();
        vela.SaldoReservado.Should().Be(2);
        vela.SaldoAtual.Should().Be(5);
        VerificarNadaPersistido();
    }

    [Fact]
    public async Task RegistrarExecucaoEnfileirada_MoveParaEmFila_EIgnoraDuplicado()
    {
        var (os, _) = OsComReserva(StatusOrdemDeServico.AguardandoPagamento);

        await Sut.RegistrarExecucaoEnfileiradaAsync(os.Id, default);
        await Sut.RegistrarExecucaoEnfileiradaAsync(os.Id, default);

        os.Status.Should().Be(StatusOrdemDeServico.EmFilaExecucao);
        _ordens.Verify(o => o.SalvarAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RegistrarStatusExecucao_EmDiagnostico_DeEmFila_Avanca()
    {
        var (os, _) = OsComReserva(StatusOrdemDeServico.EmFilaExecucao);

        var aplicado = await Sut.RegistrarStatusExecucaoAsync(os.Id, EtapaExecucao.EmDiagnostico, default);

        aplicado.Should().BeTrue();
        os.Status.Should().Be(StatusOrdemDeServico.EmDiagnostico);
    }

    [Fact]
    public async Task RegistrarStatusExecucao_EmReparoAntesDeDiagnostico_AvancaDireto()
    {
        var (os, _) = OsComReserva(StatusOrdemDeServico.EmFilaExecucao);

        var aplicado = await Sut.RegistrarStatusExecucaoAsync(os.Id, EtapaExecucao.EmReparo, default);

        aplicado.Should().BeTrue();
        os.Status.Should().Be(StatusOrdemDeServico.EmReparo);
        _ordens.Verify(o => o.SalvarAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RegistrarStatusExecucao_EmDiagnosticoAposReparo_Ignora()
    {
        var (os, _) = OsComReserva(StatusOrdemDeServico.EmReparo);

        var aplicado = await Sut.RegistrarStatusExecucaoAsync(os.Id, EtapaExecucao.EmDiagnostico, default);

        aplicado.Should().BeFalse();
        os.Status.Should().Be(StatusOrdemDeServico.EmReparo);
        VerificarNadaPersistido();
    }

    [Fact]
    public async Task RegistrarStatusExecucao_EmDiagnosticoAposFinalizada_Ignora()
    {
        var (os, _) = OsComReserva(StatusOrdemDeServico.Finalizada);

        var aplicado = await Sut.RegistrarStatusExecucaoAsync(os.Id, EtapaExecucao.EmDiagnostico, default);

        aplicado.Should().BeFalse();
        os.Status.Should().Be(StatusOrdemDeServico.Finalizada);
        VerificarNadaPersistido();
    }

    [Fact]
    public async Task RegistrarStatusExecucao_StatusRepetido_DevolveFalseSemPersistir()
    {
        var (os, _) = OsComReserva(StatusOrdemDeServico.EmDiagnostico);

        var aplicado = await Sut.RegistrarStatusExecucaoAsync(os.Id, EtapaExecucao.EmDiagnostico, default);

        aplicado.Should().BeFalse();
        os.Status.Should().Be(StatusOrdemDeServico.EmDiagnostico);
        VerificarNadaPersistido();
    }

    [Fact]
    public async Task RegistrarExecucaoFinalizada_DeEmFila_Finaliza()
    {
        var (os, _) = OsComReserva(StatusOrdemDeServico.EmFilaExecucao);

        await Sut.RegistrarExecucaoFinalizadaAsync(os.Id, default);
        await Sut.RegistrarExecucaoFinalizadaAsync(os.Id, default);

        os.Status.Should().Be(StatusOrdemDeServico.Finalizada);
        _ordens.Verify(o => o.SalvarAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Cancelar_ComReservaAtiva_LiberaReservaECancela()
    {
        var (os, peca) = OsComReserva(StatusOrdemDeServico.AguardandoPagamento);

        await Sut.CancelarAsync(os.Id, Motivo, default);

        peca.SaldoReservado.Should().Be(0);
        peca.SaldoAtual.Should().Be(10);
        os.Status.Should().Be(StatusOrdemDeServico.Cancelada);
        os.MotivoCancelamento.Should().Be(Motivo);
        _pecas.Verify(p => p.MarcarMovimentacaoComoNova(It.Is<MovimentacaoEstoque>(m =>
            m.Tipo == TipoMovimentacao.LiberacaoReserva)), Times.Once);
        _ordens.Verify(o => o.SalvarAsync(It.IsAny<CancellationToken>()), Times.Once);
        _publicador.Verify(p => p.Publicar(It.Is<EventoOrdemServico>(e => e.StatusNovo == "Cancelada")), Times.Once);
        _notificacoes.Verify(n => n.NotificarMudancaDeStatusAsync(os, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Cancelar_ComEstoqueBaixado_RepoeEstoque()
    {
        var (os, peca) = OsComReserva(StatusOrdemDeServico.AguardandoPagamento);
        await Sut.ConfirmarPagamentoAsync(os.Id, default);

        await Sut.CancelarAsync(os.Id, Motivo, default);

        peca.SaldoAtual.Should().Be(10);
        peca.SaldoReservado.Should().Be(0);
        os.Status.Should().Be(StatusOrdemDeServico.Cancelada);
        _pecas.Verify(p => p.MarcarMovimentacaoComoNova(It.Is<MovimentacaoEstoque>(m =>
            m.Tipo == TipoMovimentacao.Reposicao)), Times.Once);
    }

    [Fact]
    public async Task Cancelar_ComEstoqueBaixado_Repetido_RepoeUmaVez()
    {
        var (os, peca) = OsComReserva(StatusOrdemDeServico.AguardandoPagamento);
        await Sut.ConfirmarPagamentoAsync(os.Id, default);

        await Sut.CancelarAsync(os.Id, Motivo, default);
        await Sut.CancelarAsync(os.Id, Motivo, default);

        peca.SaldoAtual.Should().Be(10);
        _pecas.Verify(p => p.MarcarMovimentacaoComoNova(It.Is<MovimentacaoEstoque>(m =>
            m.Tipo == TipoMovimentacao.Reposicao)), Times.Once);
    }

    [Fact]
    public async Task Cancelar_OsJaCancelada_NaoMexeNoEstoque()
    {
        var (os, peca) = OsComReserva(StatusOrdemDeServico.AguardandoPagamento);

        await Sut.CancelarAsync(os.Id, Motivo, default);
        await Sut.CancelarAsync(os.Id, "outro motivo", default);

        peca.SaldoReservado.Should().Be(0);
        peca.SaldoAtual.Should().Be(10);
        os.MotivoCancelamento.Should().Be(Motivo);
        _pecas.Verify(p => p.MarcarMovimentacaoComoNova(It.IsAny<MovimentacaoEstoque>()), Times.Exactly(1));
        _ordens.Verify(o => o.SalvarAsync(It.IsAny<CancellationToken>()), Times.Once);
        _publicador.Verify(p => p.Publicar(It.IsAny<EventoOrdemServico>()), Times.Once);
    }

    [Fact]
    public async Task Cancelar_OsLegadaComSaidaManualSemReserva_NaoLancaECancela()
    {
        // OS da Fase 3: estoque saiu por saída manual com o OrdemServicoId, sem reserva —
        // a situação lê "Confirmada", mas não há o que repor (Repor lançaria 422).
        var peca = Peca.Criar(Sku.Criar("FIL-001"), "Filtro", 50m);
        peca.RegistrarEntrada(10, "Compra");
        var os = OrdemDeServico.Criar(_cliente.Id, Guid.NewGuid());
        os.AdicionarItemPeca(peca.Id, peca.Nome, 50m, 3);
        peca.RegistrarSaida(3, "Saída manual Fase 3", os.Id);
        peca.SituacaoReserva(os.Id).Should().Be(SituacaoReservaPeca.Confirmada);
        Registrar(os, peca);

        await Sut.CancelarAsync(os.Id, Motivo, default);

        os.Status.Should().Be(StatusOrdemDeServico.Cancelada);
        peca.SaldoAtual.Should().Be(7);
        _pecas.Verify(p => p.MarcarMovimentacaoComoNova(It.IsAny<MovimentacaoEstoque>()), Times.Never);
    }

    [Fact]
    public async Task Cancelar_OsEmStatusNaoCancelavel_LancaSemMexerNoEstoque()
    {
        var (os, peca) = OsComReserva(StatusOrdemDeServico.AguardandoPagamento);
        await Sut.ConfirmarPagamentoAsync(os.Id, default);
        OrdemEmStatus.Avancar(os, StatusOrdemDeServico.EmReparo);
        _pecas.Invocations.Clear();
        _ordens.Invocations.Clear();

        var act = async () => await Sut.CancelarAsync(os.Id, Motivo, default);

        await act.Should().ThrowAsync<TransicaoDeStatusInvalidaException>();
        peca.SaldoAtual.Should().Be(7);
        _pecas.Verify(p => p.MarcarMovimentacaoComoNova(It.IsAny<MovimentacaoEstoque>()), Times.Never);
        _ordens.Verify(o => o.SalvarAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RegistrarOrcamentoRejeitado_CancelaComMotivoELiberaReserva()
    {
        var (os, peca) = OsComReserva(StatusOrdemDeServico.AguardandoAprovacao);

        await Sut.RegistrarOrcamentoRejeitadoAsync(os.Id, "Caro demais", default);

        os.Status.Should().Be(StatusOrdemDeServico.Cancelada);
        os.MotivoCancelamento.Should().Be("Caro demais");
        peca.SaldoReservado.Should().Be(0);
        _pecas.Verify(p => p.MarcarMovimentacaoComoNova(It.Is<MovimentacaoEstoque>(m =>
            m.Tipo == TipoMovimentacao.LiberacaoReserva)), Times.Once);
        _ordens.Verify(o => o.SalvarAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RegistrarOrcamentoRejeitado_SemMotivo_UsaPadrao()
    {
        var (os, _) = OsComReserva(StatusOrdemDeServico.AguardandoAprovacao);

        await Sut.RegistrarOrcamentoRejeitadoAsync(os.Id, null, default);

        os.MotivoCancelamento.Should().Be(OrdemDeServico.MotivoRejeicaoPadrao);
    }

    [Fact]
    public async Task RegistrarOrcamentoRejeitado_RepetidoEDepoisCancelar_LiberaUmaVez()
    {
        var (os, peca) = OsComReserva(StatusOrdemDeServico.AguardandoAprovacao);

        await Sut.RegistrarOrcamentoRejeitadoAsync(os.Id, "Caro demais", default);
        await Sut.RegistrarOrcamentoRejeitadoAsync(os.Id, "Caro demais", default);
        await Sut.CancelarAsync(os.Id, Motivo, default);

        peca.SaldoReservado.Should().Be(0);
        peca.SaldoAtual.Should().Be(10);
        os.MotivoCancelamento.Should().Be("Caro demais");
        _pecas.Verify(p => p.MarcarMovimentacaoComoNova(It.IsAny<MovimentacaoEstoque>()), Times.Once);
    }
}
