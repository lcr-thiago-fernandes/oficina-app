using FluentAssertions;
using Moq;
using Oficina.Aplicacao.Estoque;
using Oficina.Aplicacao.Estoque.Dtos;
using Oficina.Dominio.Estoque;
using Xunit;

namespace Oficina.Aplicacao.Testes.Estoque;

public class CriarPecaUseCaseTestes
{
    private readonly Mock<IPecaRepositorio> _repo = new();

    [Fact]
    public async Task Executar_ComDadosValidos_DeveCriarERetornar()
    {
        _repo.Setup(r => r.ExisteSkuAsync(It.IsAny<Sku>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var resp = await new CriarPecaUseCase(_repo.Object)
            .ExecutarAsync(new CriarPecaRequest("ABC-123", "Filtro", 25m), default);

        resp.Sku.Should().Be("ABC-123");
        resp.SaldoAtual.Should().Be(0);
        _repo.Verify(r => r.AdicionarAsync(It.IsAny<Peca>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Executar_ComSkuJaExistente_DeveLancar()
    {
        _repo.Setup(r => r.ExisteSkuAsync(It.IsAny<Sku>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var act = async () => await new CriarPecaUseCase(_repo.Object)
            .ExecutarAsync(new CriarPecaRequest("ABC-123", "X", 10m), default);

        await act.Should().ThrowAsync<SkuJaCadastradoException>();
    }
}
