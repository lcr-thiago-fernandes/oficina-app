using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Oficina.Infraestrutura.Auth;
using Xunit;

namespace Oficina.Integracao.Testes;

public class MetricasPrometheusTestes : IClassFixture<MetricasPrometheusTestes.MetricasFactory>
{
    private readonly MetricasFactory _factory;

    public MetricasPrometheusTestes(MetricasFactory factory) => _factory = factory;

    [Fact]
    public async Task GetMetrics_DeveRetornar200_NoFormatoPrometheus()
    {
        var client = _factory.CreateClient();

        // Gera ao menos uma métrica de request antes de raspar.
        await client.GetAsync("/health");

        var response = await client.GetAsync("/metrics");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        // O formato de exposição Prometheus sempre traz linhas de metadados "# HELP"/"# TYPE".
        body.Should().Contain("# HELP");
        body.Should().Contain("# TYPE");
    }

    /// <summary>
    /// Sobe o host sem Docker/Postgres: remove o BootstrapAdminHostedService
    /// (que tentaria migrar) e fornece JWT via env. /metrics não toca em banco.
    /// </summary>
    public class MetricasFactory : WebApplicationFactory<Program>
    {
        public MetricasFactory()
        {
            // Mesmas constantes da AuthFixture: essas env vars são process-wide,
            // então valores divergentes aqui fariam o host validar contra outro
            // issuer/chave e rejeitar os tokens assinados por GeradorTokenDeTeste.
            Environment.SetEnvironmentVariable("Jwt__Secret", Auth.GeradorTokenDeTeste.Secret);
            Environment.SetEnvironmentVariable("Jwt__Issuer", Auth.GeradorTokenDeTeste.Issuer);
            Environment.SetEnvironmentVariable("Jwt__Audience", Auth.GeradorTokenDeTeste.Audience);
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                var hosted = services.SingleOrDefault(d =>
                    d.ServiceType == typeof(IHostedService) &&
                    d.ImplementationType == typeof(BootstrapAdminHostedService));
                if (hosted is not null) services.Remove(hosted);
            });
        }
    }
}
