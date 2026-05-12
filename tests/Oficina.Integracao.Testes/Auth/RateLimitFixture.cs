using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;
using Xunit;

namespace Oficina.Integracao.Testes.Auth;

// Fixture isolada para testes que precisam do rate limit REAL (5/15min).
// Não pode compartilhar AuthFixture porque a outra desliga o limit.
public class RateLimitFixture : IAsyncLifetime
{
    public PostgreSqlContainer Postgres { get; } = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("oficina")
        .WithUsername("oficina")
        .WithPassword("oficina")
        .Build();

    public WebApplicationFactory<Program> Factory { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await Postgres.StartAsync();

        Environment.SetEnvironmentVariable("ConnectionStrings__Default", Postgres.GetConnectionString());
        Environment.SetEnvironmentVariable("Jwt__Secret", new string('a', 64));
        Environment.SetEnvironmentVariable("Jwt__Issuer", "oficina-api-test");
        Environment.SetEnvironmentVariable("Jwt__Audience", "oficina-clients-test");
        Environment.SetEnvironmentVariable("AdminBootstrap__Password", "AlteraMe@123");
        Environment.SetEnvironmentVariable("RateLimit__Login__PermitLimit", "5");
        Environment.SetEnvironmentVariable("RateLimit__Login__WindowMinutes", "15");

        Factory = new WebApplicationFactory<Program>();
        _ = Factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        await Factory.DisposeAsync();
        await Postgres.DisposeAsync();
    }
}

[CollectionDefinition(nameof(RateLimitCollection))]
public class RateLimitCollection : ICollectionFixture<RateLimitFixture> { }
