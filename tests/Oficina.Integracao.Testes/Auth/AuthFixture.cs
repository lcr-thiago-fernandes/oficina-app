using MassTransit;
using MassTransit.Testing;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Oficina.Dominio.OrdensServico;
using Oficina.Infraestrutura.Mensageria;
using Oficina.Infraestrutura.Persistencia;
using Oficina.Integracao.Testes.Saga;
using Oficina.Testes.Compartilhado;
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

    /// <summary>Bus da Api nos testes: o in-memory do harness, com o registro de produção (saga EF + outbox).</summary>
    public ITestHarness Harness { get; private set; } = null!;

    /// <summary>Comandos que a saga enviou e tentativas de consumo (ver <see cref="ObservadorDaSaga"/>).</summary>
    public ObservadorDaSaga Observador { get; } = new();

    /// <summary>Falhas de banco que um teste pode armar (inerte por padrão).</summary>
    public InjetorDeFalhasNoBanco InjetorDeFalhas { get; private set; } = null!;

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

        var conexao = Postgres.GetConnectionString();
        InjetorDeFalhas = new InjetorDeFalhasNoBanco(conexao);

        Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.ConfigureTestServices(s =>
        {
            // Mesmas opções do AdicionarInfraestrutura, mais o interceptador de falhas.
            s.RemoveAll<DbContextOptions<OficinaDbContext>>();
            s.AddDbContext<OficinaDbContext>(o => o
                .UseNpgsql(conexao, npgsql => npgsql.MigrationsHistoryTable("__migrations_history", "public"))
                .AddInterceptors(InjetorDeFalhas));

            s.AddMassTransitTestHarness(x =>
            {
                // A fixture vive a coleção inteira: o timeout do harness não pode vencer no meio dela.
                x.SetTestTimeouts(testTimeout: TimeSpan.FromMinutes(30));
                DependencyInjectionMensageria.ConfigurarRegistro(x, new OpcoesMensageria { IntervaloOutboxMs = 100 });
                // Registrado depois do ConfigurarRegistro: fica por dentro do retry e conta cada tentativa.
                x.AddConfigureEndpointsCallback((_, _, cfg) => cfg.UseFilter(Observador));
            });
        }));

        // dispara o startup, que aplica migrations, cria admin e sobe o bus
        _ = Factory.CreateClient();
        Harness = Factory.Services.GetRequiredService<ITestHarness>();
        // Conecta os observadores do harness (Published/Sent/Consumed); o bus já foi iniciado pelo host.
        await Harness.Start();
        Observador.ConectarComandos(Harness.Bus, Factory.Services.GetRequiredService<NomesMensageria>());
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

    /// <summary>
    /// Leva a OS até <paramref name="alvo"/> aplicando a cadeia de transições do domínio
    /// direto no banco — na Fase 4 essas transições chegam pela saga (Billing/Execução), não
    /// por HTTP. Cadeia: Recebida → AguardandoAprovacao → AguardandoPagamento → EmFilaExecucao
    /// → EmDiagnostico → EmReparo → Finalizada → Entregue; Cancelada via rejeição do orçamento.
    /// </summary>
    public async Task LevarOrdemAteStatusNoBancoAsync(Guid ordemId, StatusOrdemDeServico alvo)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Oficina.Infraestrutura.Persistencia.OficinaDbContext>();
        var ordem = await db.OrdensServico
            .Include(o => o.ItensServico)
            .Include(o => o.ItensPeca)
            .Include(o => o.Historico.OrderBy(h => h.OcorridoEm))
            .FirstAsync(o => o.Id == ordemId);

        OrdemEmStatus.Avancar(ordem, alvo);

        await db.SaveChangesAsync();
    }
}

[CollectionDefinition(nameof(AuthCollection))]
public class AuthCollection : ICollectionFixture<AuthFixture> { }
