using System.Net.Http.Headers;
using MassTransit.Testing;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Oficina.Infraestrutura.Mensageria;
using Oficina.Integracao.Testes.Auth;
using Oficina.Integracao.Testes.Saga;
using Oficina.Testes.Compartilhado;
using Reqnroll;
using Testcontainers.PostgreSql;

namespace Oficina.Bdd.Testes.Suporte;

/// <summary>
/// Uma Api real por execução: PostgreSQL em container, saga com repositório EF e outbox, sobre o bus in-memory
/// do test harness. Billing e Execução não existem aqui: os comandos que a saga envia a eles são capturados pelo
/// <see cref="ObservadorDaSaga"/> (endpoints nas filas de comando de cada um) e as respostas são publicadas pelos
/// passos em <see cref="Harness"/>. Cada cenário cria seu serviço, peça e OS, então o banco é compartilhado.
/// </summary>
[Binding]
public sealed class AmbienteBdd
{
    private static PostgreSqlContainer? _postgres;
    private static WebApplicationFactory<Program>? _factory;

    /// <summary>Intervalo do outbox curto para os comandos chegarem em milissegundos (produção: 1 s).</summary>
    private static readonly OpcoesMensageria OpcoesMensageria = new() { IntervaloOutboxMs = 100 };

    public static WebApplicationFactory<Program> Factory => _factory ?? throw new InvalidOperationException("Ambiente não iniciado.");

    /// <summary>Cliente HTTP com token de Admin (o papel do atendente).</summary>
    public static HttpClient ClienteAdmin { get; private set; } = null!;

    /// <summary>
    /// Só para publicar os eventos do Billing e da Execução: o host já iniciou o bus, então Sent/Consumed do
    /// harness não são observados (e o outbox entrega serializado) — o que a saga enviou vem do <see cref="Observador"/>.
    /// </summary>
    public static ITestHarness Harness { get; private set; } = null!;

    public static ObservadorDaSaga Observador { get; } = new();

    [BeforeTestRun]
    public static async Task IniciarAsync()
    {
        _postgres = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("oficina")
            .WithUsername("oficina")
            .WithPassword("oficina")
            .Build();
        await _postgres.StartAsync();

        // Antes de criar a factory: Program lê a connection string durante o CreateBuilder.
        Environment.SetEnvironmentVariable("ConnectionStrings__Default", _postgres.GetConnectionString());
        Environment.SetEnvironmentVariable("Jwt__Secret", GeradorTokenDeTeste.Secret);
        Environment.SetEnvironmentVariable("Jwt__Issuer", GeradorTokenDeTeste.Issuer);
        Environment.SetEnvironmentVariable("Jwt__Audience", GeradorTokenDeTeste.Audience);
        Environment.SetEnvironmentVariable("AdminBootstrap__Password", "AlteraMe@123");

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
            b.ConfigureTestServices(s => s.SubstituirMensageriaPeloHarness(OpcoesMensageria)));

        // O primeiro cliente dispara o startup: migrations, admin e bus.
        ClienteAdmin = _factory.CreateClient();
        ClienteAdmin.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", GeradorTokenDeTeste.Gerar("Admin", Guid.NewGuid()));
        Harness = _factory.Services.GetRequiredService<ITestHarness>();
        Observador.ConectarComandos(Harness.Bus, _factory.Services.GetRequiredService<NomesMensageria>());
    }

    [AfterTestRun]
    public static async Task EncerrarAsync()
    {
        if (_factory is not null) await _factory.DisposeAsync();
        if (_postgres is not null) await _postgres.DisposeAsync();
    }
}
