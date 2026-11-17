using FluentAssertions;
using Oficina.Infraestrutura.Mensageria;

namespace Oficina.Infraestrutura.Testes.Mensageria;

public class NomesMensageriaTestes
{
    [Fact]
    public void NomesMensageria_ComPrefixoHml_GeraNomesDoSpec()
    {
        var nomes = new NomesMensageria("hml");

        nomes.FilaComandosBilling.Should().Be("hml-oficina-billing-comandos");
        nomes.FilaComandosExecucao.Should().Be("hml-oficina-execucao-comandos");
        nomes.FilaSaga.Should().Be("hml-oficina-os-saga");
        nomes.FilaBus.Should().Be("hml-oficina-os-bus");
        nomes.NomeTopico("OrcamentoGerado").Should().Be("hml-oficina-saga-OrcamentoGerado");
        nomes.NomeTopicoInterno("OrdemServicoAberta").Should().Be("hml-oficina-os-OrdemServicoAberta");
        nomes.EnderecoComandosBilling.Should().Be(new Uri("queue:hml-oficina-billing-comandos"));
        nomes.EnderecoComandosExecucao.Should().Be(new Uri("queue:hml-oficina-execucao-comandos"));
        new[] { nomes.FilaComandosBilling, nomes.FilaComandosExecucao, nomes.FilaSaga, nomes.FilaBus }
            .Should().OnlyContain(n => n.StartsWith("hml-"));
    }

    [Theory]
    [InlineData("HML")]
    [InlineData("")]
    [InlineData("a-b")]
    public void NomesMensageria_PrefixoInvalido_Lanca(string prefixo)
    {
        var criar = () => new NomesMensageria(prefixo);

        criar.Should().Throw<ArgumentException>();
    }
}
