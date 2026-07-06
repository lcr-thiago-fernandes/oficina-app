using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Oficina.Adaptadores.Auth.Controllers;
using Oficina.Aplicacao.Auth;
using Oficina.Aplicacao.Auth.Gateways;
using Oficina.Dominio.Auth;
using Oficina.Dominio.ServicosCompartilhados;

namespace Oficina.Adaptadores.Testes.Auth;

public class AutenticacaoControllerTestes
{
    private readonly Mock<IUsuarioGateway> _gateway = new();
    private readonly Mock<IGeradorTokenJwt> _gerador = new();

    private AutenticacaoController CriarController() =>
        new(new LoginUseCase(_gateway.Object, _gerador.Object, NullLogger<LoginUseCase>.Instance));

    [Fact]
    public async Task LoginAsync_ComCredenciaisCorretas_DeveRetornarSucessoComToken()
    {
        var usuario = Usuario.Criar(Username.Criar("admin"),
            Senha.DeTextoPuro("AlteraMe@123"), Perfil.Admin);
        _gateway.Setup(g => g.ObterPorUsernameAsync(It.IsAny<Username>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(usuario);
        _gerador.Setup(g => g.Gerar(usuario)).Returns(new TokenJwt("tok", 3600));

        var resultado = await CriarController()
            .LoginAsync(new LoginRequest("admin", "AlteraMe@123"), default);

        resultado.Should().BeOfType<ResultadoLogin.Sucesso>()
            .Which.Response.AccessToken.Should().Be("tok");
    }

    [Fact]
    public async Task LoginAsync_ComUsuarioInexistente_DeveRetornarCredenciaisInvalidas()
    {
        _gateway.Setup(g => g.ObterPorUsernameAsync(It.IsAny<Username>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Usuario?)null);

        var resultado = await CriarController()
            .LoginAsync(new LoginRequest("naoexiste", "qualquer8"), default);

        resultado.Should().BeOfType<ResultadoLogin.CredenciaisInvalidas>();
    }

    [Fact]
    public async Task LoginAsync_ComUsuarioInativo_DeveRetornarUsuarioInativo()
    {
        var usuario = Usuario.Criar(Username.Criar("admin"),
            Senha.DeTextoPuro("AlteraMe@123"), Perfil.Admin);
        usuario.Inativar();
        _gateway.Setup(g => g.ObterPorUsernameAsync(It.IsAny<Username>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(usuario);

        var resultado = await CriarController()
            .LoginAsync(new LoginRequest("admin", "AlteraMe@123"), default);

        resultado.Should().BeOfType<ResultadoLogin.UsuarioInativo>();
    }
}
