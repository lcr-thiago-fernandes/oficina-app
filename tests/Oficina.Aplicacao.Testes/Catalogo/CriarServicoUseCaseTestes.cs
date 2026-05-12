using FluentAssertions;
using Moq;
using Oficina.Aplicacao.Catalogo;
using Oficina.Aplicacao.Catalogo.Dtos;
using Oficina.Dominio.Catalogo;
using Xunit;

namespace Oficina.Aplicacao.Testes.Catalogo;

public class CriarServicoUseCaseTestes
{
    private readonly Mock<IServicoRepositorio> _repo = new();

    [Fact]
    public async Task Executar_ComDadosValidos_DevePersistirERetornarResponse()
    {
        var req = new CriarServicoRequest("Troca de óleo", "desc", 150m, 30);

        var resp = await new CriarServicoUseCase(_repo.Object).ExecutarAsync(req, default);

        resp.Nome.Should().Be("Troca de óleo");
        resp.PrecoBase.Should().Be(150m);
        resp.Ativo.Should().BeTrue();
        _repo.Verify(r => r.AdicionarAsync(It.IsAny<Servico>(), It.IsAny<CancellationToken>()), Times.Once);
        _repo.Verify(r => r.SalvarAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Executar_ComPrecoZero_DevePropagarServicoInvalidoException()
    {
        var req = new CriarServicoRequest("X", "desc", 0m, 30);

        var act = async () => await new CriarServicoUseCase(_repo.Object).ExecutarAsync(req, default);

        await act.Should().ThrowAsync<ServicoInvalidoException>();
    }
}
