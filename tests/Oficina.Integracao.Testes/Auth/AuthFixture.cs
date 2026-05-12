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

        Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, cfg) =>
            {
                cfg.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Default"] = Postgres.GetConnectionString(),
                    ["Jwt:Secret"] = new string('a', 64),
                    ["Jwt:Issuer"] = "oficina-api-test",
                    ["Jwt:Audience"] = "oficina-clients-test",
                    ["AdminBootstrap:Password"] = "AlteraMe@123"
                });
            });
        });

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
