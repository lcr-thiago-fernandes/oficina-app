using FluentAssertions;
using Moq;
using Oficina.Aplicacao.Clientes.Gateways;
using Oficina.Aplicacao.OrdensServico;
using Oficina.Aplicacao.OrdensServico.Dtos;
using Oficina.Aplicacao.OrdensServico.Gateways;
using Oficina.Aplicacao.OrdensServico.Telemetria;
using Oficina.Dominio.Clientes;
using Oficina.Dominio.OrdensServico;
using Xunit;

namespace Oficina.Aplicacao.Testes.OrdensServico;

public class CriarOrdemUseCaseTestes
{
    private readonly Mock<IOrdemDeServicoGateway> _gateway = new();
    private readonly Mock<IClienteGateway> _clientes = new();
    private readonly Mock<IPublicadorEventoOs> _publicador = new();

    private CriarOrdemUseCase CriarUseCase() =>
        new(_gateway.Object, _clientes.Object, _publicador.Object);

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

        var ordem = await CriarUseCase()
            .ExecutarAsync(new CriarOrdemRequest(cliente.Id, veiculo.Id, "obs"), default);

        ordem.Status.Should().Be(StatusOrdemDeServico.Recebida);
        _gateway.Verify(r => r.AdicionarAsync(It.IsAny<OrdemDeServico>(), It.IsAny<CancellationToken>()), Times.Once);
        // Volume diário de OS (painel obrigatório) depende deste evento de criação:
        // statusAnterior=null (projetado como "(inicial)" pelo publicador) → statusNovo="Recebida".
        _publicador.Verify(p => p.Publicar(
            It.Is<EventoOrdemServico>(e => e.StatusAnterior == null
                && e.StatusNovo == "Recebida"
                && e.Resultado == EventoOrdemServico.ResultadoSucesso)),
            Times.Once);
    }

    [Fact]
    public async Task Executar_ComClienteInexistente_DeveLancar()
    {
        _clientes.Setup(c => c.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Cliente?)null);

        var act = async () => await CriarUseCase()
            .ExecutarAsync(new CriarOrdemRequest(Guid.NewGuid(), Guid.NewGuid(), null), default);

        await act.Should().ThrowAsync<OrdemInvalidaException>().WithMessage("*Cliente*");
        // Falha antes de existir uma OS — nada para reportar em telemetria.
        _publicador.Verify(p => p.Publicar(It.IsAny<EventoOrdemServico>()), Times.Never);
    }

    [Fact]
    public async Task Executar_ComVeiculoQueNaoPertenceAoCliente_DeveLancar()
    {
        var cliente = Cliente.Criar("J", Documento.Criar("39053344705"),
            Email.Criar("e@x.com"), Telefone.Criar("11987654321"));
        cliente.AdicionarVeiculo(Veiculo.Criar(Placa.Criar("ABC1234"), "F", "U", 2020));

        _clientes.Setup(c => c.ObterPorIdAsync(cliente.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cliente);

        var act = async () => await CriarUseCase()
            .ExecutarAsync(new CriarOrdemRequest(cliente.Id, Guid.NewGuid(), null), default);

        await act.Should().ThrowAsync<OrdemInvalidaException>().WithMessage("*Veículo*");
        _publicador.Verify(p => p.Publicar(It.IsAny<EventoOrdemServico>()), Times.Never);
    }

    [Fact]
    public async Task Executar_FalhaAoSalvar_PublicaEventoDeFalhaEPropaga()
    {
        var cliente = Cliente.Criar("J", Documento.Criar("39053344705"),
            Email.Criar("e@x.com"), Telefone.Criar("11987654321"));
        var veiculo = Veiculo.Criar(Placa.Criar("ABC1234"), "F", "U", 2020);
        cliente.AdicionarVeiculo(veiculo);

        _clientes.Setup(c => c.ObterPorIdAsync(cliente.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cliente);
        _gateway.Setup(g => g.SalvarAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("falha simulada de persistência"));

        var act = async () => await CriarUseCase()
            .ExecutarAsync(new CriarOrdemRequest(cliente.Id, veiculo.Id, "obs"), default);

        await act.Should().ThrowAsync<InvalidOperationException>();
        _publicador.Verify(p => p.Publicar(
            It.Is<EventoOrdemServico>(e => e.Resultado == EventoOrdemServico.ResultadoFalha)), Times.Once);
    }
}
