using FluentAssertions;
using Moq;
using Oficina.Aplicacao.Catalogo;
using Oficina.Aplicacao.Catalogo.Dtos;
using Oficina.Dominio.Catalogo;
using Xunit;

namespace Oficina.Aplicacao.Testes.Catalogo;

public class AtualizarServicoUseCaseTestes
{
    private readonly Mock<IServicoRepositorio> _repo = new();

    [Fact]
    public async Task Executar_ComServicoExistente_DeveAtualizarERetornar()
    {
        var s = Servico.Criar("Antigo", "old", 50m, 15);
        _repo.Setup(r => r.ObterPorIdAsync(s.Id, It.IsAny<CancellationToken>())).ReturnsAsync(s);

        var resp = await new AtualizarServicoUseCase(_repo.Object).ExecutarAsync(
            s.Id, new AtualizarServicoRequest("Novo", "new", 99.90m, 45), default);

        resp.Should().NotBeNull();
        resp!.Nome.Should().Be("Novo");
        resp.PrecoBase.Should().Be(99.90m);
    }

    [Fact]
    public async Task Executar_ComServicoInexistente_DeveRetornarNull()
    {
        _repo.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Servico?)null);

        var resp = await new AtualizarServicoUseCase(_repo.Object).ExecutarAsync(
            Guid.NewGuid(), new AtualizarServicoRequest("X", "y", 10m, 30), default);

        resp.Should().BeNull();
    }
}
