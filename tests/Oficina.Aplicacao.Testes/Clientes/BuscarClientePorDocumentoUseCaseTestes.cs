using FluentAssertions;
using Moq;
using Oficina.Aplicacao.Clientes;
using Oficina.Aplicacao.Clientes.Gateways;
using Oficina.Dominio.Clientes;
using Xunit;

namespace Oficina.Aplicacao.Testes.Clientes;

public class BuscarClientePorDocumentoUseCaseTestes
{
    private readonly Mock<IClienteGateway> _gateway = new();

    [Fact]
    public async Task Executar_ComDocumentoValidoEExistente_DeveRetornarCliente()
    {
        var c = Cliente.Criar("Joao", Documento.Criar("39053344705"),
            Email.Criar("a@b.com"), Telefone.Criar("11987654321"));

        _gateway.Setup(r => r.ObterPorDocumentoAsync(It.IsAny<Documento>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(c);

        var resp = await new BuscarClientePorDocumentoUseCase(_gateway.Object)
            .ExecutarAsync("390.533.447-05", default);

        resp.Should().NotBeNull();
        resp!.Documento.Valor.Should().Be("39053344705");
    }

    [Fact]
    public async Task Executar_ComDocumentoInexistente_DeveRetornarNull()
    {
        _gateway.Setup(r => r.ObterPorDocumentoAsync(It.IsAny<Documento>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Cliente?)null);

        var resp = await new BuscarClientePorDocumentoUseCase(_gateway.Object)
            .ExecutarAsync("39053344705", default);

        resp.Should().BeNull();
    }

    [Fact]
    public async Task Executar_ComDocumentoInvalido_DevePropagarExcecao()
    {
        var act = async () => await new BuscarClientePorDocumentoUseCase(_gateway.Object)
            .ExecutarAsync("11111111111", default);

        await act.Should().ThrowAsync<DocumentoInvalidoException>();
    }
}
