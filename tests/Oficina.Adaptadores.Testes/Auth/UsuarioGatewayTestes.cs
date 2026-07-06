using FluentAssertions;
using Moq;
using Oficina.Adaptadores.Auth.DataSources;
using Oficina.Adaptadores.Auth.Gateways;
using Oficina.Dominio.Auth;

namespace Oficina.Adaptadores.Testes.Auth;

public class UsuarioGatewayTestes
{
    private static Usuario CriarUsuario() =>
        Usuario.Criar(Username.Criar("admin"), Senha.DeTextoPuro("AlteraMe@123"), Perfil.Admin);

    [Fact]
    public async Task ObterPorUsernameAsync_DeveDelegarParaODataSource()
    {
        var ds = new Mock<IUsuarioDataSource>();
        var esperado = CriarUsuario();
        ds.Setup(d => d.ObterPorUsernameAsync(It.IsAny<Username>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(esperado);

        var gateway = new UsuarioGateway(ds.Object);
        var obtido = await gateway.ObterPorUsernameAsync(esperado.Username, default);

        obtido.Should().BeSameAs(esperado);
        ds.Verify(d => d.ObterPorUsernameAsync(esperado.Username, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExisteAsync_DeveDelegarParaODataSource()
    {
        var ds = new Mock<IUsuarioDataSource>();
        ds.Setup(d => d.ExisteAsync(It.IsAny<Username>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var gateway = new UsuarioGateway(ds.Object);
        var existe = await gateway.ExisteAsync(Username.Criar("admin"), default);

        existe.Should().BeTrue();
        ds.Verify(d => d.ExisteAsync(It.IsAny<Username>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AdicionarESalvar_DevemDelegarParaODataSource()
    {
        var ds = new Mock<IUsuarioDataSource>();
        var gateway = new UsuarioGateway(ds.Object);
        var usuario = CriarUsuario();

        await gateway.AdicionarAsync(usuario, default);
        await gateway.SalvarAsync(default);

        ds.Verify(d => d.AdicionarAsync(usuario, It.IsAny<CancellationToken>()), Times.Once);
        ds.Verify(d => d.SalvarAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
