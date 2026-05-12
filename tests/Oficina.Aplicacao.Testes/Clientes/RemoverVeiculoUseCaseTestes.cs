using FluentAssertions;
using Moq;
using Oficina.Aplicacao.Clientes;
using Oficina.Dominio.Clientes;
using Xunit;

namespace Oficina.Aplicacao.Testes.Clientes;

public class RemoverVeiculoUseCaseTestes
{
    private readonly Mock<IClienteRepositorio> _repo = new();

    [Fact]
    public async Task Executar_ComPlacaInexistente_DeveLancar()
    {
        var cliente = Cliente.Criar("J", Documento.Criar("39053344705"),
            Email.Criar("a@b.com"), Telefone.Criar("11987654321"));

        _repo.Setup(r => r.ObterPorIdAsync(cliente.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cliente);

        var act = async () => await new RemoverVeiculoUseCase(_repo.Object)
            .ExecutarAsync(cliente.Id, "ZZZ9999", default);

        await act.Should().ThrowAsync<VeiculoNaoEncontradoException>();
    }
}
