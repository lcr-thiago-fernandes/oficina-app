using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Oficina.Aplicacao.Auth;
using Oficina.Aplicacao.Auth.Gateways;
using Oficina.Dominio.Auth;
using Oficina.Dominio.ServicosCompartilhados;
using Xunit;

namespace Oficina.Aplicacao.Testes.Auth;

public class LoginUseCaseTestes
{
    private readonly Mock<IUsuarioGateway> _gateway = new();
    private readonly Mock<IGeradorTokenJwt> _gerador = new();

    private LoginUseCase Construir() =>
        new(_gateway.Object, _gerador.Object, NullLogger<LoginUseCase>.Instance);

    [Fact]
    public async Task Executar_ComCredenciaisCorretas_DeveRetornarSucesso()
    {
        var senha = Senha.DeTextoPuro("AlteraMe@123");
        var u = Usuario.Criar(Username.Criar("admin"), senha, Perfil.Admin);

        _gateway.Setup(r => r.ObterPorUsernameAsync(
                It.Is<Username>(x => x.Valor == "admin"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(u);

        _gerador.Setup(g => g.Gerar(u)).Returns(new TokenJwt("tok", 3600));

        var resultado = await Construir().ExecutarAsync(
            new LoginRequest("admin", "AlteraMe@123"), default);

        resultado.Should().BeOfType<ResultadoLogin.Sucesso>()
            .Which.Response.AccessToken.Should().Be("tok");
    }

    [Fact]
    public async Task Executar_ComUsuarioInexistente_DeveRetornarCredenciaisInvalidas()
    {
        _gateway.Setup(r => r.ObterPorUsernameAsync(It.IsAny<Username>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Usuario?)null);

        var resultado = await Construir().ExecutarAsync(
            new LoginRequest("naoexiste", "qualquer8"), default);

        resultado.Should().BeOfType<ResultadoLogin.CredenciaisInvalidas>();
    }

    [Fact]
    public async Task Executar_ComSenhaErrada_DeveRetornarCredenciaisInvalidas()
    {
        var u = Usuario.Criar(Username.Criar("admin"),
            Senha.DeTextoPuro("AlteraMe@123"), Perfil.Admin);

        _gateway.Setup(r => r.ObterPorUsernameAsync(It.IsAny<Username>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(u);

        var resultado = await Construir().ExecutarAsync(
            new LoginRequest("admin", "ErradoXYZ@9"), default);

        resultado.Should().BeOfType<ResultadoLogin.CredenciaisInvalidas>();
    }

    [Fact]
    public async Task Executar_ComUsuarioInativo_DeveRetornarUsuarioInativo()
    {
        var u = Usuario.Criar(Username.Criar("admin"),
            Senha.DeTextoPuro("AlteraMe@123"), Perfil.Admin);
        u.Inativar();

        _gateway.Setup(r => r.ObterPorUsernameAsync(It.IsAny<Username>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(u);

        var resultado = await Construir().ExecutarAsync(
            new LoginRequest("admin", "AlteraMe@123"), default);

        resultado.Should().BeOfType<ResultadoLogin.UsuarioInativo>();
    }

    [Fact]
    public async Task Executar_ComUsernameInvalido_DeveRetornarCredenciaisInvalidas()
    {
        var resultado = await Construir().ExecutarAsync(
            new LoginRequest("us@", "AlteraMe@123"), default);

        resultado.Should().BeOfType<ResultadoLogin.CredenciaisInvalidas>();
    }
}
