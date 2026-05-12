using FluentAssertions;
using Moq;
using Oficina.Aplicacao.Consulta;
using Oficina.Aplicacao.Consulta.Dtos;
using Oficina.Dominio.Clientes;
using Oficina.Dominio.OrdensServico;
using Xunit;

namespace Oficina.Aplicacao.Testes.Consulta;

public class ConsultarOrdemPorNumeroUseCaseTestes
{
    private readonly Mock<IOrdemDeServicoRepositorio> _ordens = new();
    private readonly Mock<IClienteRepositorio> _clientes = new();

    [Fact]
    public async Task Executar_ComDocumentoCorreto_DeveRetornarSucesso()
    {
        var cliente = Cliente.Criar("Maria", Documento.Criar("39053344705"),
            Email.Criar("m@x.com"), Telefone.Criar("11987654321"));
        var v = Veiculo.Criar(Placa.Criar("ABC1234"), "Fiat", "Uno", 2020);
        cliente.AdicionarVeiculo(v);

        var os = OrdemDeServico.Criar(cliente.Id, v.Id);
        os.AdicionarItemServico(Guid.NewGuid(), "Troca de óleo", 150m, 1);

        _ordens.Setup(r => r.ObterPorNumeroAsync(123, It.IsAny<CancellationToken>())).ReturnsAsync(os);
        _clientes.Setup(c => c.ObterPorIdAsync(cliente.Id, It.IsAny<CancellationToken>())).ReturnsAsync(cliente);

        var resultado = await new ConsultarOrdemPorNumeroUseCase(_ordens.Object, _clientes.Object)
            .ExecutarAsync(123, "390.533.447-05", default);

        resultado.Should().BeOfType<ResultadoConsulta.Sucesso>();
        var s = (ResultadoConsulta.Sucesso)resultado;
        s.Response.ClienteNome.Should().Be("Maria");
        s.Response.DocumentoMascarado.Should().Be("390.533.447-05");
        s.Response.VeiculoPlaca.Should().Be("ABC1234");
        s.Response.Itens.Should().HaveCount(1);
    }

    [Fact]
    public async Task Executar_ComOrdemInexistente_DeveRetornarNaoEncontrada()
    {
        _ordens.Setup(r => r.ObterPorNumeroAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((OrdemDeServico?)null);

        var resultado = await new ConsultarOrdemPorNumeroUseCase(_ordens.Object, _clientes.Object)
            .ExecutarAsync(999, "39053344705", default);

        resultado.Should().BeOfType<ResultadoConsulta.NaoEncontrada>();
    }

    [Fact]
    public async Task Executar_ComDocumentoErrado_DeveRetornarDocumentoNaoConfere()
    {
        var cliente = Cliente.Criar("Maria", Documento.Criar("39053344705"),
            Email.Criar("m@x.com"), Telefone.Criar("11987654321"));
        cliente.AdicionarVeiculo(Veiculo.Criar(Placa.Criar("ABC1234"), "F", "U", 2020));
        var os = OrdemDeServico.Criar(cliente.Id, cliente.Veiculos.First().Id);

        _ordens.Setup(r => r.ObterPorNumeroAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(os);
        _clientes.Setup(c => c.ObterPorIdAsync(cliente.Id, It.IsAny<CancellationToken>())).ReturnsAsync(cliente);

        var resultado = await new ConsultarOrdemPorNumeroUseCase(_ordens.Object, _clientes.Object)
            .ExecutarAsync(1, "11144477735", default); // outro CPF válido

        resultado.Should().BeOfType<ResultadoConsulta.DocumentoNaoConfere>();
    }

    [Fact]
    public async Task Executar_ComDocumentoMalformado_DeveRetornarNaoEncontrada()
    {
        // documento inválido: para evitar enumeração/timing attack, tratamos como
        // "não encontrada". Nunca retornamos 422 nessa rota.
        var resultado = await new ConsultarOrdemPorNumeroUseCase(_ordens.Object, _clientes.Object)
            .ExecutarAsync(1, "11111111111", default);

        resultado.Should().BeOfType<ResultadoConsulta.NaoEncontrada>();
    }
}
