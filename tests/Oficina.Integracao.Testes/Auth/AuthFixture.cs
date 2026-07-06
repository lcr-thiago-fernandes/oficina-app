using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Oficina.Aplicacao.Auth;
using Testcontainers.PostgreSql;
using Xunit;

namespace Oficina.Integracao.Testes.Auth;

public class AuthFixture : IAsyncLifetime
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

        // Env vars precisam ser definidas ANTES de instanciar WebApplicationFactory,
        // porque Program.cs lê ConnectionStrings:Default durante CreateBuilder (síncrono),
        // e isso acontece antes de qualquer callback de WithWebHostBuilder rodar.
        Environment.SetEnvironmentVariable("ConnectionStrings__Default", Postgres.GetConnectionString());
        Environment.SetEnvironmentVariable("Jwt__Secret", new string('a', 64));
        Environment.SetEnvironmentVariable("Jwt__Issuer", "oficina-api-test");
        Environment.SetEnvironmentVariable("Jwt__Audience", "oficina-clients-test");
        Environment.SetEnvironmentVariable("AdminBootstrap__Password", "AlteraMe@123");
        Environment.SetEnvironmentVariable("Webhook__Token", "token-teste-webhook");

        // Rate limit dos demais testes desligado na prática — RateLimitTestes
        // usa fixture separada com limite real.
        Environment.SetEnvironmentVariable("RateLimit__Login__PermitLimit", "100000");
        Environment.SetEnvironmentVariable("RateLimit__Login__WindowMinutes", "1");

        Factory = new WebApplicationFactory<Program>();

        // dispara o startup, que aplica migrations e cria admin
        _ = Factory.CreateClient();
    }

    public async Task<string> ObterTokenAdminAsync()
    {
        var client = Factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { username = "admin", password = "AlteraMe@123" });
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<LoginResponse>();
        return body!.AccessToken;
    }

    public async Task DisposeAsync()
    {
        await Factory.DisposeAsync();
        await Postgres.DisposeAsync();
    }

    public async Task AprovarOrcamentoDiretoNoBancoAsync(Guid ordemId)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Oficina.Infraestrutura.Persistencia.OficinaDbContext>();
        var ordem = await db.OrdensServico.FirstAsync(o => o.Id == ordemId);
        ordem.Aprovar();
        await db.SaveChangesAsync();
    }
}

[CollectionDefinition(nameof(AuthCollection))]
public class AuthCollection : ICollectionFixture<AuthFixture> { }
