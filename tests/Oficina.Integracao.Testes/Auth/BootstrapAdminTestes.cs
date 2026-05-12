using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Oficina.Dominio.Auth;
using Oficina.Infraestrutura.Persistencia;
using Xunit;

namespace Oficina.Integracao.Testes.Auth;

[Collection(nameof(AuthCollection))]
public class BootstrapAdminTestes
{
    private readonly AuthFixture _fx;

    public BootstrapAdminTestes(AuthFixture fx) => _fx = fx;

    [Fact]
    public async Task Apos_Startup_DeveExistirUsuarioAdminComFlagDeTroca()
    {
        using var scope = _fx.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OficinaDbContext>();

        var admin = db.Usuarios.AsEnumerable()
            .SingleOrDefault(u => u.Username.Valor == "admin");

        admin.Should().NotBeNull();
        admin!.PrecisaTrocarSenha.Should().BeTrue();
        admin.Perfil.Should().Be(Perfil.Admin);
        admin.Ativo.Should().BeTrue();

        await Task.CompletedTask;
    }
}
