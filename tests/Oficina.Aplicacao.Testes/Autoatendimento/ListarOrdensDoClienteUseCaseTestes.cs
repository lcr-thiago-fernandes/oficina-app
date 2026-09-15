using FluentAssertions;
using Moq;
using Oficina.Aplicacao.Autoatendimento;
using Oficina.Aplicacao.Clientes.Gateways;
using Oficina.Aplicacao.OrdensServico.Gateways;
using Oficina.Dominio.Clientes;
using Oficina.Dominio.OrdensServico;
using Xunit;

namespace Oficina.Aplicacao.Testes.Autoatendimento;

public class ListarOrdensDoClienteUseCaseTestes
{
    private readonly Mock<IOrdemDeServicoGateway> _ordens = new();
    private readonly Mock<IClienteGateway> _clientes = new();

    private ListarOrdensDoClienteUseCase Sut => new(_ordens.Object, _clientes.Object);

    private static Cliente ClienteValido(string doc = "11144477735") =>
        Cliente.Criar("Fulano", Documento.Criar(doc),
            Email.Criar("f@x.com"), Telefone.Criar("11987654321"));

    [Fact]
    public async Task Retorna_nulo_quando_o_documento_e_invalido()
    {
        var r = await Sut.ExecutarAsync("123", CancellationToken.None);
        r.Should().BeNull();
    }

    [Fact]
    public async Task Retorna_nulo_quando_o_cliente_nao_existe()
    {
        _clientes.Setup(c => c.ObterPorDocumentoAsync(It.IsAny<Documento>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync((Cliente?)null);

        var r = await Sut.ExecutarAsync("11144477735", CancellationToken.None);

        r.Should().BeNull();
    }

    [Fact]
    public async Task Mapeia_as_ordens_do_cliente_para_o_resumo()
    {
        var cliente = ClienteValido();
        var os = OrdemDeServico.Criar(cliente.Id, Guid.NewGuid(), null, "filial-sp");
        os.IniciarDiagnostico();

        _clientes.Setup(c => c.ObterPorDocumentoAsync(It.IsAny<Documento>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(cliente);
        _ordens.Setup(o => o.ListarPorClienteAsync(cliente.Id, It.IsAny<CancellationToken>()))
               .ReturnsAsync(new[] { os });

        var r = await Sut.ExecutarAsync("11144477735", CancellationToken.None);

        r.Should().HaveCount(1);
        r![0].Numero.Should().Be(os.Numero);
        r[0].Status.Should().Be("EmDiagnostico");
        r[0].Unidade.Should().Be("filial-sp");
    }
}
