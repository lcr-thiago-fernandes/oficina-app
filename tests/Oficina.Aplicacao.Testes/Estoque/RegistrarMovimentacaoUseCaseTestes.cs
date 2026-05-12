using FluentAssertions;
using Moq;
using Oficina.Aplicacao.Estoque;
using Oficina.Aplicacao.Estoque.Dtos;
using Oficina.Dominio.Estoque;
using Xunit;

namespace Oficina.Aplicacao.Testes.Estoque;

public class RegistrarMovimentacaoUseCaseTestes
{
    private readonly Mock<IPecaRepositorio> _repo = new();

    private void ConfigurarTransacaoIdentidade()
    {
        _repo.Setup(r => r.EmTransacaoSerializadaAsync(
                It.IsAny<Func<CancellationToken, Task>>(),
                It.IsAny<CancellationToken>()))
            .Returns<Func<CancellationToken, Task>, CancellationToken>((f, ct) => f(ct));
    }

    [Fact]
    public async Task Executar_Entrada_DeveAumentarSaldoEPersistir()
    {
        var p = Peca.Criar(Sku.Criar("ABC-123"), "Filtro", 10m);
        _repo.Setup(r => r.ObterPorIdAsync(p.Id, It.IsAny<CancellationToken>())).ReturnsAsync(p);
        ConfigurarTransacaoIdentidade();

        var resp = await new RegistrarMovimentacaoUseCase(_repo.Object).ExecutarAsync(
            p.Id, new RegistrarMovimentacaoRequest("Entrada", 5, "Compra", null), default);

        resp.Should().NotBeNull();
        p.SaldoAtual.Should().Be(5);
        _repo.Verify(r => r.SalvarAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Executar_SaidaComSaldoSuficiente_DeveDiminuir()
    {
        var p = Peca.Criar(Sku.Criar("ABC-123"), "Filtro", 10m);
        p.RegistrarEntrada(10, "compra inicial");
        _repo.Setup(r => r.ObterPorIdAsync(p.Id, It.IsAny<CancellationToken>())).ReturnsAsync(p);
        ConfigurarTransacaoIdentidade();

        var resp = await new RegistrarMovimentacaoUseCase(_repo.Object).ExecutarAsync(
            p.Id, new RegistrarMovimentacaoRequest("Saida", 3, "OS", Guid.NewGuid()), default);

        resp.Should().NotBeNull();
        p.SaldoAtual.Should().Be(7);
    }

    [Fact]
    public async Task Executar_SaidaComSaldoInsuficiente_DeveLancar()
    {
        var p = Peca.Criar(Sku.Criar("ABC-123"), "Filtro", 10m);
        p.RegistrarEntrada(2, "compra");
        _repo.Setup(r => r.ObterPorIdAsync(p.Id, It.IsAny<CancellationToken>())).ReturnsAsync(p);
        ConfigurarTransacaoIdentidade();

        var act = async () => await new RegistrarMovimentacaoUseCase(_repo.Object).ExecutarAsync(
            p.Id, new RegistrarMovimentacaoRequest("Saida", 5, "x", null), default);

        await act.Should().ThrowAsync<SaldoInsuficienteException>();
        p.SaldoAtual.Should().Be(2); // não mexeu
    }

    [Fact]
    public async Task Executar_PecaInexistente_DeveRetornarNull()
    {
        _repo.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Peca?)null);
        ConfigurarTransacaoIdentidade();

        var resp = await new RegistrarMovimentacaoUseCase(_repo.Object).ExecutarAsync(
            Guid.NewGuid(), new RegistrarMovimentacaoRequest("Entrada", 1, "x", null), default);

        resp.Should().BeNull();
    }

    [Fact]
    public async Task Executar_TipoInvalido_DeveLancar()
    {
        ConfigurarTransacaoIdentidade();
        var act = async () => await new RegistrarMovimentacaoUseCase(_repo.Object).ExecutarAsync(
            Guid.NewGuid(), new RegistrarMovimentacaoRequest("Bagulho", 1, "x", null), default);

        await act.Should().ThrowAsync<MovimentacaoInvalidaException>();
    }
}
