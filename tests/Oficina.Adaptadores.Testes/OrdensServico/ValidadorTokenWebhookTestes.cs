using FluentAssertions;
using Oficina.Adaptadores.OrdensServico.Webhooks;

namespace Oficina.Adaptadores.Testes.OrdensServico;

public class ValidadorTokenWebhookTestes
{
    [Fact]
    public void TokenCorreto_DeveSerValido()
    {
        ValidadorTokenWebhook.EhTokenValido("segredo-123", "segredo-123").Should().BeTrue();
    }

    [Fact]
    public void TokenErrado_DeveSerInvalido()
    {
        ValidadorTokenWebhook.EhTokenValido("errado", "segredo-123").Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void TokenRecebidoAusente_DeveSerInvalido(string? recebido)
    {
        ValidadorTokenWebhook.EhTokenValido(recebido, "segredo-123").Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void TokenEsperadoNaoConfigurado_DeveSerInvalido_FailClosed(string? esperado)
    {
        ValidadorTokenWebhook.EhTokenValido("qualquer", esperado).Should().BeFalse();
    }

    [Fact]
    public void TamanhosDiferentes_DeveSerInvalido()
    {
        ValidadorTokenWebhook.EhTokenValido("abc", "abcdef").Should().BeFalse();
    }
}
