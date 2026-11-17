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
        nomes.FilaBus("oficina-os-7d9f8c6b5-x2k4q").Should().Be("hml-oficina-os-bus-oficina-os-7d9f8c6b5-x2k4q");
        nomes.NomeTopico("OrcamentoGerado").Should().Be("hml-oficina-saga-OrcamentoGerado");
        nomes.NomeTopicoInterno("OrdemServicoAberta").Should().Be("hml-oficina-os-OrdemServicoAberta");
        nomes.EnderecoComandosBilling.Should().Be(new Uri("queue:hml-oficina-billing-comandos"));
        nomes.EnderecoComandosExecucao.Should().Be(new Uri("queue:hml-oficina-execucao-comandos"));
        new[] { nomes.FilaComandosBilling, nomes.FilaComandosExecucao, nomes.FilaSaga, nomes.FilaBus("pod") }
            .Should().OnlyContain(n => n.StartsWith("hml-"));
    }

    [Theory]
    [InlineData("MAQUINA-01", "hml-oficina-os-bus-maquina-01")]
    [InlineData("host.local", "hml-oficina-os-bus-host-local")]
    [InlineData("pc do joão_1", "hml-oficina-os-bus-pc-do-jo-o_1")]
    public void FilaBus_SanitizaInstancia(string instancia, string esperado) =>
        new NomesMensageria("hml").FilaBus(instancia).Should().Be(esperado);

    [Fact]
    public void FilaBus_NomeLongo_TruncaNoLimiteDoSqs()
    {
        var nome = new NomesMensageria("prd").FilaBus(new string('a', 200));

        nome.Should().HaveLength(NomesMensageria.TamanhoMaximoFilaSqs).And.HaveLength(80);
        nome.Should().StartWith("prd-oficina-os-bus-aaa");
    }

    [Fact]
    public void InstanciaAtual_UsaHostnameOuNomeDaMaquina()
    {
        var anterior = Environment.GetEnvironmentVariable("HOSTNAME");
        try
        {
            Environment.SetEnvironmentVariable("HOSTNAME", "pod-abc");
            NomesMensageria.InstanciaAtual().Should().Be("pod-abc");

            Environment.SetEnvironmentVariable("HOSTNAME", null);
            NomesMensageria.InstanciaAtual().Should().Be(Environment.MachineName);
        }
        finally
        {
            Environment.SetEnvironmentVariable("HOSTNAME", anterior);
        }
    }

    [Theory]
    [InlineData("local")]
    [InlineData("hml")]
    [InlineData("prd")]
    [InlineData("dev-2")]
    public void NomesMensageria_PrefixoValido_Aceita(string prefixo)
    {
        var criar = () => new NomesMensageria(prefixo);

        criar.Should().NotThrow();
    }

    [Theory]
    [InlineData("HML")]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("hml\n")]
    [InlineData("hml_1")]
    [InlineData("prd.oficina")]
    [InlineData("Prod!")]
    public void NomesMensageria_PrefixoInvalido_Lanca(string prefixo)
    {
        var criar = () => new NomesMensageria(prefixo);

        criar.Should().Throw<ArgumentException>().WithMessage("*PrefixoAmbiente*");
    }
}
