using FluentAssertions;
using Moq;
using Oficina.Aplicacao.Consulta;
using Oficina.Aplicacao.Consulta.Dtos;
using Oficina.Dominio.Clientes;
using Oficina.Dominio.OrdensServico;
using Xunit;

namespace Oficina.Aplicacao.Testes.Consulta;

public class AprovarOrcamentoPorClienteUseCaseTestes
{
    private readonly Mock<IOrdemDeServicoRepositorio> _ordens = new();
    private readonly Mock<IClienteRepositorio> _clientes = new();

    private (Cliente cliente, OrdemDeServico os) Cenario()
    {
        var cliente = Cliente.Criar("M", Documento.Criar("39053344705"),
            Email.Criar("m@x.com"), Telefone.Criar("11987654321"));
        var v = Veiculo.Criar(Placa.Criar("ABC1234"), "F", "U", 2020);
        cliente.AdicionarVeiculo(v);

        var os = OrdemDeServico.Criar(cliente.Id, v.Id);
        os.AdicionarItemServico(Guid.NewGuid(), "S", 10m, 1);
        os.IniciarDiagnostico();
        os.EnviarOrcamentoParaAprovacao();
        return (cliente, os);
    }

    [Fact]
    public async Task Executar_DeveAprovarECommitar()
    {
        var (cliente, os) = Cenario();
        _ordens.Setup(r => r.ObterPorNumeroAsync(123, It.IsAny<CancellationToken>())).ReturnsAsync(os);
        _clientes.Setup(c => c.ObterPorIdAsync(cliente.Id, It.IsAny<CancellationToken>())).ReturnsAsync(cliente);

        var r = await new AprovarOrcamentoPorClienteUseCase(_ordens.Object, _clientes.Object)
            .ExecutarAsync(123, "39053344705", default);

        r.Should().BeOfType<ResultadoConsulta.Sucesso>();
        os.OrcamentoAprovadoEm.Should().NotBeNull();
        _ordens.Verify(r => r.SalvarAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Executar_OsInexistente_DeveRetornarNaoEncontrada()
    {
        _ordens.Setup(r => r.ObterPorNumeroAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((OrdemDeServico?)null);

        var r = await new AprovarOrcamentoPorClienteUseCase(_ordens.Object, _clientes.Object)
            .ExecutarAsync(1, "39053344705", default);

        r.Should().BeOfType<ResultadoConsulta.NaoEncontrada>();
    }

    [Fact]
    public async Task Executar_DocumentoErrado_DeveRetornarDocumentoNaoConfere()
    {
        var (cliente, os) = Cenario();
        _ordens.Setup(r => r.ObterPorNumeroAsync(123, It.IsAny<CancellationToken>())).ReturnsAsync(os);
        _clientes.Setup(c => c.ObterPorIdAsync(cliente.Id, It.IsAny<CancellationToken>())).ReturnsAsync(cliente);

        var r = await new AprovarOrcamentoPorClienteUseCase(_ordens.Object, _clientes.Object)
            .ExecutarAsync(123, "11144477735", default);

        r.Should().BeOfType<ResultadoConsulta.DocumentoNaoConfere>();
        os.OrcamentoAprovadoEm.Should().BeNull();
    }

    [Fact]
    public async Task Executar_OrdemNaoAguardandoAprovacao_DevePropagarTransicaoInvalida()
    {
        var cliente = Cliente.Criar("M", Documento.Criar("39053344705"),
            Email.Criar("m@x.com"), Telefone.Criar("11987654321"));
        cliente.AdicionarVeiculo(Veiculo.Criar(Placa.Criar("ABC1234"), "F", "U", 2020));
        var os = OrdemDeServico.Criar(cliente.Id, cliente.Veiculos.First().Id);
        // OS está em Recebida — não deveria poder aprovar

        _ordens.Setup(r => r.ObterPorNumeroAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(os);
        _clientes.Setup(c => c.ObterPorIdAsync(cliente.Id, It.IsAny<CancellationToken>())).ReturnsAsync(cliente);

        var act = async () => await new AprovarOrcamentoPorClienteUseCase(_ordens.Object, _clientes.Object)
            .ExecutarAsync(1, "39053344705", default);

        await act.Should().ThrowAsync<TransicaoDeStatusInvalidaException>();
    }
}
