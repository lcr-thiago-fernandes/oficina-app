using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Oficina.Aplicacao.Auth;
using Xunit;

namespace Oficina.Integracao.Testes.Auth;

[Collection(nameof(AuthCollection))]
public class RateLimitTestes
{
    private readonly AuthFixture _fx;

    public RateLimitTestes(AuthFixture fx) => _fx = fx;

    [Fact]
    public async Task Login_AposCincoFalhas_DeveRetornar429()
    {
        var client = _fx.Factory.CreateClient();
        var req = new LoginRequest("admin", "ErradoXYZ@9");

        for (var i = 0; i < 5; i++)
        {
            var r = await client.PostAsJsonAsync("/api/v1/auth/login", req);
            r.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        var sexta = await client.PostAsJsonAsync("/api/v1/auth/login", req);
        sexta.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }
}
