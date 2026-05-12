using FluentAssertions;
using Moq;
using Oficina.Aplicacao.Clientes;
using Oficina.Aplicacao.Clientes.Dtos;
using Oficina.Dominio.Clientes;
using Xunit;

namespace Oficina.Aplicacao.Testes.Clientes;

public class AdicionarVeiculoUseCaseTestes
{
    private readonly Mock<IClienteRepositorio> _repo = new();

    [Fact]
    public async Task Executar_ComClienteExistente_DeveRetornarVeiculo()
    {
        var cliente = Cliente.Criar("J", Documento.Criar("39053344705"),
            Email.Criar("a@b.com"), Telefone.Criar("11987654321"));

        _repo.Setup(r => r.ObterPorIdAsync(cliente.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cliente);

        var req = new AdicionarVeiculoRequest("ABC1234", "Fiat", "Uno", 2020);
        var resp = await new AdicionarVeiculoUseCase(_repo.Object).ExecutarAsync(cliente.Id, req, default);

        resp.Should().NotBeNull();
        resp!.Placa.Should().Be("ABC1234");
        cliente.Veiculos.Should().HaveCount(1);
    }

    [Fact]
    public async Task Executar_ComClienteInexistente_DeveRetornarNull()
    {
        _repo.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Cliente?)null);

        var req = new AdicionarVeiculoRequest("ABC1234", "Fiat", "Uno", 2020);
        var resp = await new AdicionarVeiculoUseCase(_repo.Object)
            .ExecutarAsync(Guid.NewGuid(), req, default);

        resp.Should().BeNull();
    }

    [Fact]
    public async Task Executar_ComPlacaJaExistente_DevePropagarExcecao()
    {
        var cliente = Cliente.Criar("J", Documento.Criar("39053344705"),
            Email.Criar("a@b.com"), Telefone.Criar("11987654321"));
        cliente.AdicionarVeiculo(Veiculo.Criar(Placa.Criar("ABC1234"), "F", "U", 2020));

        _repo.Setup(r => r.ObterPorIdAsync(cliente.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cliente);

        var req = new AdicionarVeiculoRequest("ABC1234", "VW", "Gol", 2018);
        var act = async () => await new AdicionarVeiculoUseCase(_repo.Object)
            .ExecutarAsync(cliente.Id, req, default);

        await act.Should().ThrowAsync<PlacaJaCadastradaException>();
    }
}
