using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Oficina.Aplicacao.Auth;
using Oficina.Aplicacao.Auth.Gateways;
using Oficina.Dominio.Auth;
using Xunit;

namespace Oficina.Aplicacao.Testes.Auth;

public class BootstrapAdminUseCaseTestes
{
    private readonly Mock<IUsuarioGateway> _gateway = new();

    private BootstrapAdminUseCase Construir() =>
        new(_gateway.Object, NullLogger<BootstrapAdminUseCase>.Instance);

    [Fact]
    public async Task Executar_QuandoAdminNaoExiste_DeveCriarComBootstrap()
    {
        _gateway.Setup(r => r.ExisteAsync(It.IsAny<Username>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        await Construir().ExecutarAsync("AlteraMe@123", default);

        _gateway.Verify(r => r.AdicionarAsync(
            It.Is<Usuario>(u => u.Username.Valor == "admin" && u.PrecisaTrocarSenha),
            It.IsAny<CancellationToken>()), Times.Once);
        _gateway.Verify(r => r.SalvarAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Executar_QuandoAdminExiste_DeveSerNoOp()
    {
        _gateway.Setup(r => r.ExisteAsync(It.IsAny<Username>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await Construir().ExecutarAsync("AlteraMe@123", default);

        _gateway.Verify(r => r.AdicionarAsync(It.IsAny<Usuario>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Executar_ComSenhaVazia_DeveLancar()
    {
        var act = async () => await Construir().ExecutarAsync("", default);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*ADMIN_BOOTSTRAP_PASSWORD*");
    }
}
