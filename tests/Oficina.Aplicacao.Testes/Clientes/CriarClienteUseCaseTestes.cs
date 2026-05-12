using FluentAssertions;
using Moq;
using Oficina.Aplicacao.Clientes;
using Oficina.Aplicacao.Clientes.Dtos;
using Oficina.Dominio.Clientes;
using Xunit;

namespace Oficina.Aplicacao.Testes.Clientes;

public class CriarClienteUseCaseTestes
{
    private readonly Mock<IClienteRepositorio> _repo = new();

    private CriarClienteUseCase Construir() => new(_repo.Object);

    [Fact]
    public async Task Executar_ComDadosValidos_DeveCriarERetornarResponse()
    {
        _repo.Setup(r => r.ExisteDocumentoAsync(It.IsAny<Documento>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var req = new CriarClienteRequest("João", "39053344705", "joao@x.com", "11987654321");
        var resp = await Construir().ExecutarAsync(req, default);

        resp.Nome.Should().Be("João");
        resp.Documento.Should().Be("39053344705");
        resp.TipoPessoa.Should().Be("PF");
        _repo.Verify(r => r.AdicionarAsync(It.IsAny<Cliente>(), It.IsAny<CancellationToken>()), Times.Once);
        _repo.Verify(r => r.SalvarAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Executar_ComDocumentoJaExistente_DeveLancar()
    {
        _repo.Setup(r => r.ExisteDocumentoAsync(It.IsAny<Documento>(), It.IsAny<CancellationToken>()))
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
