using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Oficina.Aplicacao.Auth;
using Xunit;

namespace Oficina.Integracao.Testes.Auth;

[Collection(nameof(AuthCollection))]
public class LoginEndpointTestes
{
    private readonly AuthFixture _fx;

    public LoginEndpointTestes(AuthFixture fx) => _fx = fx;

    [Fact]
    public async Task Login_ComCredenciaisDeBootstrap_DeveRetornar200ETokenComFlag()
    {
        var client = _fx.Factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/api/v1/auth/login",
            new LoginRequest("admin", "AlteraMe@123"));

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadFromJsonAsync<LoginResponse>();
        body!.AccessToken.Should().NotBeNullOrWhiteSpace();
        body.PrecisaTrocarSenha.Should().BeTrue();
        body.ExpiresInSeconds.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Login_ComSenhaErrada_DeveRetornar401()
    {
        var client = _fx.Factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/api/v1/auth/login",
            new LoginRequest("admin", "ErradoXYZ@9"));

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_SemBody_DeveRetornar400()
    {
        var client = _fx.Factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { username = "", password = "" });

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
