using FluentAssertions;
using Oficina.Adaptadores.Clientes.Presenters;
using Oficina.Dominio.Clientes;

namespace Oficina.Adaptadores.Testes.Clientes;

public class ClientePresenterTestes
{
    private static Cliente CriarClienteComVeiculo()
    {
        var cliente = Cliente.Criar("João", Documento.Criar("39053344705"),
            Email.Criar("joao@x.com"), Telefone.Criar("11987654321"));
        cliente.AdicionarVeiculo(Veiculo.Criar(Placa.Criar("ABC1234"), "Fiat", "Uno", 2020));
        return cliente;
    }

    [Fact]
    public void Apresentar_DeveMapearTodosOsCamposDaEntidade()
    {
        var cliente = CriarClienteComVeiculo();

        var resp = ClientePresenter.Apresentar(cliente);

        resp.Id.Should().Be(cliente.Id);
        resp.Nome.Should().Be("João");
        resp.TipoPessoa.Should().Be("PF");
        resp.Documento.Should().Be("39053344705");
        resp.DocumentoMascarado.Should().Be("390.533.447-05");
        resp.Email.Should().Be("joao@x.com");
        resp.Telefone.Should().Be("11987654321");
        resp.Ativo.Should().BeTrue();
        resp.Veiculos.Should().ContainSingle(v => v.Placa == "ABC1234");
    }

    [Fact]
    public void ApresentarVeiculo_DeveMapearCampos()
    {
        var veiculo = Veiculo.Criar(Placa.Criar("ABC1234"), "Fiat", "Uno", 2020);

        var resp = ClientePresenter.ApresentarVeiculo(veiculo);

        resp.Id.Should().Be(veiculo.Id);
        resp.Placa.Should().Be("ABC1234");
        resp.Marca.Should().Be("Fiat");
        resp.Modelo.Should().Be("Uno");
        resp.Ano.Should().Be(2020);
    }

    [Fact]
    public void ApresentarPagina_DeveMapearItensEMetadados()
    {
        var itens = new[] { CriarClienteComVeiculo() };

        var pagina = ClientePresenter.ApresentarPagina(itens, total: 1, pagina: 1, tamanhoPagina: 20);

        pagina.Total.Should().Be(1);
        pagina.Pagina.Should().Be(1);
        pagina.TamanhoPagina.Should().Be(20);
        pagina.Itens.Should().ContainSingle(c => c.Nome == "João");
    }
}
