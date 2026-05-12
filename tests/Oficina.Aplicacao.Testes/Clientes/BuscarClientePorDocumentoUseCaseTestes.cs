using FluentAssertions;
using Moq;
using Oficina.Aplicacao.Clientes;
using Oficina.Dominio.Clientes;
using Xunit;

namespace Oficina.Aplicacao.Testes.Clientes;

public class BuscarClientePorDocumentoUseCaseTestes
{
    private readonly Mock<IClienteRepositorio> _repo = new();

    [Fact]
    public async Task Executar_ComDocumentoValidoEExistente_DeveRetornarResponse()
    {
        var c = Cliente.Criar("Joao", Documento.Criar("39053344705"),
            Email.Criar("a@b.com"), Telefone.Criar("11987654321"));

        _repo.Setup(r => r.ObterPorDocumentoAsync(It.IsAny<Documento>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(c);

        var resp = await new BuscarClientePorDocumentoUseCase(_repo.Object)
            .ExecutarAsync("390.533.447-05", default);

        resp.Should().NotBeNull();
        resp!.Documento.Should().Be("39053344705");
    }

    [Fact]
    public async Task Executar_ComDocumentoInexistente_DeveRetornarNull()
    {
        _repo.Setup(r => r.ObterPorDocumentoAsync(It.IsAny<Documento>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Cliente?)null);

        var resp = await new BuscarClientePorDocumentoUseCase(_repo.Object)
            .ExecutarAsync("39053344705", default);

        resp.Should().BeNull();
    }

    [Fact]
    public async Task Executar_ComDocumentoInvalido_DevePropagarExcecao()
    {
        var act = async () => await new BuscarClientePorDocumentoUseCase(_repo.Object)
            .ExecutarAsync("11111111111", default);

        await act.Should().ThrowAsync<DocumentoInvalidoException>();
    }
}
