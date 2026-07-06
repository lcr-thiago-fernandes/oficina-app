using FluentAssertions;
using Moq;
using Oficina.Aplicacao.Catalogo.Gateways;
using Oficina.Aplicacao.Clientes.Gateways;
using Oficina.Aplicacao.Estoque.Gateways;
using Oficina.Aplicacao.OrdensServico;
using Oficina.Aplicacao.OrdensServico.Dtos;
using Oficina.Aplicacao.OrdensServico.Gateways;
using Oficina.Dominio.Catalogo;
using Oficina.Dominio.Clientes;
using Oficina.Dominio.Estoque;
using Oficina.Dominio.OrdensServico;
using Xunit;

namespace Oficina.Aplicacao.Testes.OrdensServico;

public class AbrirOrdemDeServicoUseCaseTestes
{
    private readonly Mock<IClienteGateway> _clientes = new();
    private readonly Mock<IServicoGateway> _servicos = new();
    private readonly Mock<IPecaGateway> _pecas = new();
    private readonly Mock<IOrdemDeServicoGateway> _ordens = new();

    private AbrirOrdemDeServicoUseCase CriarUseCase() =>
        new(_clientes.Object, _servicos.Object, _pecas.Object, _ordens.Object);

    // Simula o re-fetch final: devolve a mesma OS que foi adicionada.
    private void ConfigurarRefetch()
    {
        OrdemDeServico? adicionada = null;
        _ordens.Setup(o => o.AdicionarAsync(It.IsAny<OrdemDeServico>(), It.IsAny<CancellationToken>()))
            .Callback<OrdemDeServico, CancellationToken>((o, _) => adicionada = o)
            .Returns(Task.CompletedTask);
        _ordens.Setup(o => o.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => adicionada);
    }

    private static AbrirOrdemRequest RequestValido(Guid servicoId) => new(
        new ClienteDadosDto("39053344705", "João", "joao@x.com", "11987654321"),
        new VeiculoDadosDto("ABC1234", "Fiat", "Uno", 2020),
        new[] { new ItemServicoDto(servicoId, 2) },
        Array.Empty<ItemPecaDto>());

    [Fact]
    public async Task Executar_ClienteNovo_DeveCriarClienteVeiculoEOrdem()
    {
        var servico = Servico.Criar("Troca de óleo", "x", 150m, 30);
        _clientes.Setup(c => c.ObterPorDocumentoAsync(It.IsAny<Documento>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Cliente?)null);
        _servicos.Setup(s => s.ObterPorIdAsync(servico.Id, It.IsAny<CancellationToken>())).ReturnsAsync(servico);
        ConfigurarRefetch();

        var ordem = await CriarUseCase().ExecutarAsync(RequestValido(servico.Id), default);

        ordem.Should().NotBeNull();
        ordem.Status.Should().Be(StatusOrdemDeServico.Recebida);
        ordem.ItensServico.Should().ContainSingle(i => i.Subtotal == 300m);
        _clientes.Verify(c => c.AdicionarAsync(It.IsAny<Cliente>(), It.IsAny<CancellationToken>()), Times.Once);
        _clientes.Verify(c => c.MarcarVeiculoComoNovo(It.IsAny<Veiculo>()), Times.Never);
        _ordens.Verify(o => o.SalvarAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Executar_ClienteExistenteVeiculoNovo_DeveMarcarVeiculoComoNovo()
    {
        var cliente = Cliente.Criar("João", Documento.Criar("39053344705"),
            Email.Criar("joao@x.com"), Telefone.Criar("11987654321"));
        var servico = Servico.Criar("Troca de óleo", "x", 150m, 30);
        _clientes.Setup(c => c.ObterPorDocumentoAsync(It.IsAny<Documento>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(cliente);
        _servicos.Setup(s => s.ObterPorIdAsync(servico.Id, It.IsAny<CancellationToken>())).ReturnsAsync(servico);
        ConfigurarRefetch();

        var ordem = await CriarUseCase().ExecutarAsync(RequestValido(servico.Id), default);

        ordem.ClienteId.Should().Be(cliente.Id);
        cliente.Veiculos.Should().ContainSingle(v => v.Placa.Valor == "ABC1234");
        _clientes.Verify(c => c.AdicionarAsync(It.IsAny<Cliente>(), It.IsAny<CancellationToken>()), Times.Never);
        _clientes.Verify(c => c.MarcarVeiculoComoNovo(It.IsAny<Veiculo>()), Times.Once);
    }

    [Fact]
    public async Task Executar_ClienteExistenteVeiculoExistente_NaoDuplicaVeiculo()
    {
        var cliente = Cliente.Criar("João", Documento.Criar("39053344705"),
            Email.Criar("joao@x.com"), Telefone.Criar("11987654321"));
        cliente.AdicionarVeiculo(Veiculo.Criar(Placa.Criar("ABC1234"), "Fiat", "Uno", 2020));
        var servico = Servico.Criar("Troca de óleo", "x", 150m, 30);
        _clientes.Setup(c => c.ObterPorDocumentoAsync(It.IsAny<Documento>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(cliente);
        _servicos.Setup(s => s.ObterPorIdAsync(servico.Id, It.IsAny<CancellationToken>())).ReturnsAsync(servico);
        ConfigurarRefetch();

        await CriarUseCase().ExecutarAsync(RequestValido(servico.Id), default);

        cliente.Veiculos.Should().HaveCount(1);
        _clientes.Verify(c => c.MarcarVeiculoComoNovo(It.IsAny<Veiculo>()), Times.Never);
    }

    [Fact]
    public async Task Executar_ServicoInativo_DeveLancar()
    {
        var servico = Servico.Criar("Troca de óleo", "x", 150m, 30);
        servico.Inativar();
        _clientes.Setup(c => c.ObterPorDocumentoAsync(It.IsAny<Documento>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Cliente?)null);
        _servicos.Setup(s => s.ObterPorIdAsync(servico.Id, It.IsAny<CancellationToken>())).ReturnsAsync(servico);

        var act = async () => await CriarUseCase().ExecutarAsync(RequestValido(servico.Id), default);

        await act.Should().ThrowAsync<OrdemInvalidaException>().WithMessage("*inativo*");
        _ordens.Verify(o => o.SalvarAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Executar_PecaInexistente_DeveLancar()
    {
        var pecaId = Guid.NewGuid();
        _clientes.Setup(c => c.ObterPorDocumentoAsync(It.IsAny<Documento>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Cliente?)null);
        _pecas.Setup(p => p.ObterPorIdAsync(pecaId, It.IsAny<CancellationToken>())).ReturnsAsync((Peca?)null);

        var req = new AbrirOrdemRequest(
            new ClienteDadosDto("39053344705", "João", "joao@x.com", "11987654321"),
            new VeiculoDadosDto("ABC1234", "Fiat", "Uno", 2020),
            Array.Empty<ItemServicoDto>(),
            new[] { new ItemPecaDto(pecaId, 1) });

        var act = async () => await CriarUseCase().ExecutarAsync(req, default);

        await act.Should().ThrowAsync<OrdemInvalidaException>().WithMessage("*não encontrada*");
        _ordens.Verify(o => o.SalvarAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
