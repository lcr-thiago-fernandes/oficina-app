using FluentAssertions;
using Moq;
using Oficina.Aplicacao.Catalogo;
using Oficina.Aplicacao.Catalogo.Dtos;
using Oficina.Aplicacao.Catalogo.Gateways;
using Oficina.Dominio.Catalogo;
using Xunit;

namespace Oficina.Aplicacao.Testes.Catalogo;

public class AtualizarServicoUseCaseTestes
{
    private readonly Mock<IServicoGateway> _gateway = new();

    [Fact]
    public async Task Executar_ComServicoExistente_DeveAtualizarERetornar()
    {
        var s = Servico.Criar("Antigo", "old", 50m, 15);
        _gateway.Setup(g => g.ObterPorIdAsync(s.Id, It.IsAny<CancellationToken>())).ReturnsAsync(s);

        var resultado = await new AtualizarServicoUseCase(_gateway.Object).ExecutarAsync(
            s.Id, new AtualizarServicoRequest("Novo", "new", 99.90m, 45), default);

        resultado.Should().NotBeNull();
        resultado!.Nome.Should().Be("Novo");
        resultado.PrecoBase.Should().Be(99.90m);
    }

    [Fact]
    public async Task Executar_ComServicoInexistente_DeveRetornarNull()
    {
        _gateway.Setup(g => g.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Servico?)null);

        var resultado = await new AtualizarServicoUseCase(_gateway.Object).ExecutarAsync(
            Guid.NewGuid(), new AtualizarServicoRequest("X", "y", 10m, 30), default);

        resultado.Should().BeNull();
    }
}
