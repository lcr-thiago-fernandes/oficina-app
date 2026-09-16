using FluentAssertions;
using Moq;
using Oficina.Aplicacao.Autoatendimento;
using Oficina.Aplicacao.Clientes.Gateways;
using Oficina.Dominio.Clientes;
using Xunit;

namespace Oficina.Aplicacao.Testes.Autoatendimento;

public class ListarVeiculosDoClienteUseCaseTestes
{
    private readonly Mock<IClienteGateway> _clientes = new();

    private ListarVeiculosDoClienteUseCase Sut => new(_clientes.Object);

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
    public async Task Mapeia_os_veiculos_do_cliente_para_o_response()
    {
        var cliente = ClienteValido();
        var veiculo = Veiculo.Criar(Placa.Criar("ABC1D23"), "Fiat", "Uno", 2020);
        cliente.AdicionarVeiculo(veiculo);

        _clientes.Setup(c => c.ObterPorDocumentoAsync(It.IsAny<Documento>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(cliente);

        var r = await Sut.ExecutarAsync("11144477735", CancellationToken.None);

        r.Should().HaveCount(1);
        r![0].Id.Should().Be(veiculo.Id);
        r[0].Placa.Should().Be(veiculo.Placa.Valor);
        r[0].Marca.Should().Be("Fiat");
        r[0].Modelo.Should().Be("Uno");
        r[0].Ano.Should().Be(2020);
    }
}
