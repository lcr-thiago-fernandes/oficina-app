using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Oficina.Aplicacao.Auth;
using Oficina.Dominio.Auth;
using Xunit;

namespace Oficina.Aplicacao.Testes.Auth;

public class BootstrapAdminUseCaseTestes
{
    private readonly Mock<IUsuarioRepositorio> _repo = new();

    private BootstrapAdminUseCase Construir() =>
        new(_repo.Object, NullLogger<BootstrapAdminUseCase>.Instance);

    [Fact]
    public async Task Executar_QuandoAdminNaoExiste_DeveCriarComBootstrap()
    {
        _repo.Setup(r => r.ExisteAsync(It.IsAny<Username>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        await Construir().ExecutarAsync("AlteraMe@123", default);

        _repo.Verify(r => r.AdicionarAsync(
            It.Is<Usuario>(u => u.Username.Valor == "admin" && u.PrecisaTrocarSenha),
            It.IsAny<CancellationToken>()), Times.Once);
        _repo.Verify(r => r.SalvarAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Executar_QuandoAdminExiste_DeveSerNoOp()
    {
        _repo.Setup(r => r.ExisteAsync(It.IsAny<Username>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await Construir().ExecutarAsync("AlteraMe@123", default);

        _repo.Verify(r => r.AdicionarAsync(It.IsAny<Usuario>(), It.IsAny<CancellationToken>()),
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
