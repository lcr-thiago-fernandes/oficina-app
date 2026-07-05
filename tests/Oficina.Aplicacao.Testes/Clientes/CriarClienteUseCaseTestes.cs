using FluentAssertions;
using Moq;
using Oficina.Aplicacao.Clientes;
using Oficina.Aplicacao.Clientes.Dtos;
using Oficina.Aplicacao.Clientes.Gateways;
using Oficina.Dominio.Clientes;
using Xunit;

namespace Oficina.Aplicacao.Testes.Clientes;

public class CriarClienteUseCaseTestes
{
    private readonly Mock<IClienteGateway> _gateway = new();

    private CriarClienteUseCase Construir() => new(_gateway.Object);

    [Fact]
    public async Task Executar_ComDadosValidos_DeveCriarERetornarEntidade()
    {
        _gateway.Setup(r => r.ExisteDocumentoAsync(It.IsAny<Documento>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var req = new CriarClienteRequest("João", "39053344705", "joao@x.com", "11987654321");
        var cliente = await Construir().ExecutarAsync(req, default);

        cliente.Nome.Should().Be("João");
        cliente.Documento.Valor.Should().Be("39053344705");
        cliente.Documento.Tipo.Should().Be(TipoPessoa.PF);
        _gateway.Verify(r => r.AdicionarAsync(It.IsAny<Cliente>(), It.IsAny<CancellationToken>()), Times.Once);
        _gateway.Verify(r => r.SalvarAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Executar_ComDocumentoJaExistente_DeveLancar()
    {
        _gateway.Setup(r => r.ExisteDocumentoAsync(It.IsAny<Documento>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var req = new CriarClienteRequest("João", "39053344705", "joao@x.com", "11987654321");
        var act = async () => await Construir().ExecutarAsync(req, default);

        await act.Should().ThrowAsync<DocumentoJaCadastradoException>();
    }

    [Fact]
    public async Task Executar_ComDocumentoInvalido_DevePropagarExcecaoDoVO()
    {
        var req = new CriarClienteRequest("João", "11111111111", "joao@x.com", "11987654321");
        var act = async () => await Construir().ExecutarAsync(req, default);

        await act.Should().ThrowAsync<DocumentoInvalidoException>();
    }
}
