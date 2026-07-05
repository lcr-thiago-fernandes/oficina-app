using FluentAssertions;
using Moq;
using Oficina.Aplicacao.Catalogo;
using Oficina.Aplicacao.Catalogo.Dtos;
using Oficina.Aplicacao.Catalogo.Gateways;
using Oficina.Dominio.Catalogo;
using Xunit;

namespace Oficina.Aplicacao.Testes.Catalogo;

public class CriarServicoUseCaseTestes
{
    private readonly Mock<IServicoGateway> _gateway = new();

    [Fact]
    public async Task Executar_ComDadosValidos_DevePersistirERetornarEntidade()
    {
        var req = new CriarServicoRequest("Troca de óleo", "desc", 150m, 30);

        var servico = await new CriarServicoUseCase(_gateway.Object).ExecutarAsync(req, default);

        servico.Nome.Should().Be("Troca de óleo");
        servico.PrecoBase.Should().Be(150m);
        servico.Ativo.Should().BeTrue();
        _gateway.Verify(g => g.AdicionarAsync(It.IsAny<Servico>(), It.IsAny<CancellationToken>()), Times.Once);
        _gateway.Verify(g => g.SalvarAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Executar_ComPrecoZero_DevePropagarServicoInvalidoException()
    {
        var req = new CriarServicoRequest("X", "desc", 0m, 30);

        var act = async () => await new CriarServicoUseCase(_gateway.Object).ExecutarAsync(req, default);

        await act.Should().ThrowAsync<ServicoInvalidoException>();
    }
}
