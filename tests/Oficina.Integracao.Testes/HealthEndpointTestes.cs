using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Oficina.Infraestrutura.Auth;
using Xunit;

namespace Oficina.Integracao.Testes;

public class HealthEndpointTestes : IClassFixture<HealthEndpointTestes.HealthFactory>
{
    private readonly HealthFactory _factory;

    public HealthEndpointTestes(HealthFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetHealth_DeveRetornar200_ComStatusOk()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("\"status\":\"ok\"");
    }

    /// <summary>
    /// Fornece configuração mínima para o host bootar sem Docker:
    /// /health não toca em DB, mas a inicialização exige JWT e o
    /// BootstrapAdminHostedService tentaria rodar migrations em Postgres.
    /// Removemos o hosted service e fornecemos JWT via env vars.
    /// </summary>
    public class HealthFactory : WebApplicationFactory<Program>
    {
        public HealthFactory()
        {
            // Mesmas constantes da AuthFixture: essas env vars sao process-wide,
            // entao valores divergentes aqui fariam o host validar contra outro
            // issuer/chave e rejeitar os tokens assinados por GeradorTokenDeTeste.
            Environment.SetEnvironmentVariable("Jwt__Secret", Auth.GeradorTokenDeTeste.Secret);
            Environment.SetEnvironmentVariable("Jwt__Issuer", Auth.GeradorTokenDeTeste.Issuer);
            Environment.SetEnvironmentVariable("Jwt__Audience", Auth.GeradorTokenDeTeste.Audience);
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                // Remove o bootstrap para o teste de /health não exigir Postgres.
                var hosted = services.SingleOrDefault(d =>
                    d.ServiceType == typeof(IHostedService) &&
                    d.ImplementationType == typeof(BootstrapAdminHostedService));
                if (hosted is not null) services.Remove(hosted);
            });
        }
    }
}
