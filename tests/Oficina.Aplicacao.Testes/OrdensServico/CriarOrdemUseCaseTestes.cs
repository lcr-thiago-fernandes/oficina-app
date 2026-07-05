using FluentAssertions;
using Moq;
using Oficina.Aplicacao.Clientes.Gateways;
using Oficina.Aplicacao.OrdensServico;
using Oficina.Aplicacao.OrdensServico.Dtos;
using Oficina.Aplicacao.OrdensServico.Gateways;
using Oficina.Dominio.Clientes;
using Oficina.Dominio.OrdensServico;
using Xunit;

namespace Oficina.Aplicacao.Testes.OrdensServico;

public class CriarOrdemUseCaseTestes
{
    private readonly Mock<IOrdemDeServicoGateway> _gateway = new();
    private readonly Mock<IClienteGateway> _clientes = new();

    [Fact]
    public async Task Executar_ComClienteEVeiculoValidos_DeveCriar()
    {
        var cliente = Cliente.Criar("J", Documento.Criar("39053344705"),
            Email.Criar("e@x.com"), Telefone.Criar("11987654321"));
        var veiculo = Veiculo.Criar(Placa.Criar("ABC1234"), "F", "U", 2020);
        cliente.AdicionarVeiculo(veiculo);

        _clientes.Setup(c => c.ObterPorIdAsync(cliente.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cliente);

        // simular o re-fetch após criação
        _gateway.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) =>
                OrdemDeServico.Criar(cliente.Id, veiculo.Id));

        var ordem = await new CriarOrdemUseCase(_gateway.Object, _clientes.Object)
            .ExecutarAsync(new CriarOrdemRequest(cliente.Id, veiculo.Id, "obs"), default);

        ordem.Status.Should().Be(StatusOrdemDeServico.Recebida);
        _gateway.Verify(r => r.AdicionarAsync(It.IsAny<OrdemDeServico>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Executar_ComClienteInexistente_DeveLancar()
    {
        _clientes.Setup(c => c.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Cliente?)null);

        var act = async () => await new CriarOrdemUseCase(_gateway.Object, _clientes.Object)
            .ExecutarAsync(new CriarOrdemRequest(Guid.NewGuid(), Guid.NewGuid(), null), default);

        await act.Should().ThrowAsync<OrdemInvalidaException>().WithMessage("*Cliente*");
    }

    [Fact]
    public async Task Executar_ComVeiculoQueNaoPertenceAoCliente_DeveLancar()
    {
        var cliente = Cliente.Criar("J", Documento.Criar("39053344705"),
            Email.Criar("e@x.com"), Telefone.Criar("11987654321"));
        cliente.AdicionarVeiculo(Veiculo.Criar(Placa.Criar("ABC1234"), "F", "U", 2020));

        _clientes.Setup(c => c.ObterPorIdAsync(cliente.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cliente);

        var act = async () => await new CriarOrdemUseCase(_gateway.Object, _clientes.Object)
            .ExecutarAsync(new CriarOrdemRequest(cliente.Id, Guid.NewGuid(), null), default);

        await act.Should().ThrowAsync<OrdemInvalidaException>().WithMessage("*Veículo*");
    }
}
