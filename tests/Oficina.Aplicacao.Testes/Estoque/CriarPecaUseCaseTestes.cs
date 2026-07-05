using FluentAssertions;
using Moq;
using Oficina.Aplicacao.Estoque;
using Oficina.Aplicacao.Estoque.Dtos;
using Oficina.Aplicacao.Estoque.Gateways;
using Oficina.Dominio.Estoque;
using Xunit;

namespace Oficina.Aplicacao.Testes.Estoque;

public class CriarPecaUseCaseTestes
{
    private readonly Mock<IPecaGateway> _gateway = new();

    [Fact]
    public async Task Executar_ComDadosValidos_DeveCriarERetornar()
    {
        _gateway.Setup(r => r.ExisteSkuAsync(It.IsAny<Sku>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var peca = await new CriarPecaUseCase(_gateway.Object)
            .ExecutarAsync(new CriarPecaRequest("ABC-123", "Filtro", 25m), default);

        peca.Sku.Valor.Should().Be("ABC-123");
        peca.SaldoAtual.Should().Be(0);
        _gateway.Verify(r => r.AdicionarAsync(It.IsAny<Peca>(), It.IsAny<CancellationToken>()), Times.Once);
        _gateway.Verify(r => r.SalvarAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Executar_ComSkuJaExistente_DeveLancar()
    {
        _gateway.Setup(r => r.ExisteSkuAsync(It.IsAny<Sku>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var act = async () => await new CriarPecaUseCase(_gateway.Object)
            .ExecutarAsync(new CriarPecaRequest("ABC-123", "X", 10m), default);

        await act.Should().ThrowAsync<SkuJaCadastradoException>();
    }
}
