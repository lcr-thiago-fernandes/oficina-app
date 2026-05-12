using FluentAssertions;
using Oficina.Dominio.Auth;
using Xunit;

namespace Oficina.Dominio.Testes.Auth;

public class UsuarioTestes
{
    [Fact]
    public void Criar_ComDadosValidos_DeveInstanciarAtivo()
    {
        var u = Usuario.Criar(
            Username.Criar("admin"),
            Senha.DeTextoPuro("AlteraMe@123"),
            Perfil.Admin);

        u.Id.Should().NotBeEmpty();
        u.Username.Valor.Should().Be("admin");
        u.Perfil.Should().Be(Perfil.Admin);
        u.Ativo.Should().BeTrue();
        u.PrecisaTrocarSenha.Should().BeFalse();
        u.CriadoEm.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void CriarParaBootstrap_DeveExigirTrocaDeSenha()
    {
        var u = Usuario.CriarParaBootstrap(
            Username.Criar("admin"),
            Senha.DeTextoPuro("AlteraMe@123"));

        u.PrecisaTrocarSenha.Should().BeTrue();
        u.Perfil.Should().Be(Perfil.Admin);
    }

    [Fact]
    public void Inativar_DeveDefinirAtivoFalse()
    {
        var u = NovoUsuario();
        u.Inativar();
        u.Ativo.Should().BeFalse();
    }

    [Fact]
    public void Autenticar_ComSenhaCerta_DeveRetornarTrue()
    {
        var u = NovoUsuario();
        u.Autenticar("AlteraMe@123").Should().BeTrue();
    }

    [Fact]
    public void Autenticar_ComSenhaErrada_DeveRetornarFalse()
    {
        var u = NovoUsuario();
        u.Autenticar("ErradoXYZ@9").Should().BeFalse();
    }

    [Fact]
    public void Autenticar_QuandoInativo_DeveLancar()
    {
        var u = NovoUsuario();
        u.Inativar();

        var act = () => u.Autenticar("AlteraMe@123");

        act.Should().Throw<UsuarioInativoException>();
    }

    [Fact]
    public void TrocarSenha_DeveAtualizarHashERemoverPendencia()
    {
        var u = Usuario.CriarParaBootstrap(
            Username.Criar("admin"),
            Senha.DeTextoPuro("AlteraMe@123"));
        u.PrecisaTrocarSenha.Should().BeTrue();

        u.TrocarSenha(Senha.DeTextoPuro("NovaSenha@456"));

        u.PrecisaTrocarSenha.Should().BeFalse();
        u.Autenticar("NovaSenha@456").Should().BeTrue();
        u.Autenticar("AlteraMe@123").Should().BeFalse();
    }

    private static Usuario NovoUsuario() => Usuario.Criar(
        Username.Criar("admin"),
        Senha.DeTextoPuro("AlteraMe@123"),
        Perfil.Admin);
}
