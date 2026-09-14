using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
        Environment.SetEnvironmentVariable("Jwt__Secret", GeradorTokenDeTeste.Secret);
        Environment.SetEnvironmentVariable("Jwt__Issuer", GeradorTokenDeTeste.Issuer);
        Environment.SetEnvironmentVariable("Jwt__Audience", GeradorTokenDeTeste.Audience);
        Environment.SetEnvironmentVariable("AdminBootstrap__Password", "AlteraMe@123");
        Environment.SetEnvironmentVariable("Webhook__Token", "token-teste-webhook");

        Factory = new WebApplicationFactory<Program>();

        // dispara o startup, que aplica migrations e cria admin
        _ = Factory.CreateClient();
    }

    /// <summary>
    /// Token de Admin assinado localmente. A API não emite mais tokens —
    /// esse papel é da função serverless oficina-auth-api (repositório separado).
    /// </summary>
    public Task<string> ObterTokenAdminAsync() =>
        Task.FromResult(GeradorTokenDeTeste.Gerar("Admin", Guid.NewGuid()));

    /// <summary>Token de Cliente, para os endpoints de autoatendimento e de consulta.</summary>
    public Task<string> ObterTokenClienteAsync(Guid clienteId, string documento) =>
        Task.FromResult(GeradorTokenDeTeste.Gerar("Cliente", clienteId, documento));

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
