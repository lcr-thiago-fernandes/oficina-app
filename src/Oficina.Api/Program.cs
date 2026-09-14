using FluentValidation.AspNetCore;
using Oficina.Adaptadores;
using Oficina.Aplicacao;
using Oficina.Api.Configuracao;
using Oficina.Infraestrutura;
using Oficina.Infraestrutura.Persistencia;
using OpenTelemetry.Metrics;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) =>
{
    configuration.ReadFrom.Configuration(context.Configuration);
});

builder.Services.AddControllers();
builder.Services
    .AddFluentValidationAutoValidation()
    .AddFluentValidationClientsideAdapters();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "Oficina Mecânica API",
        Version = "v1",
        Description = "API do MVP do Sistema Integrado de Atendimento e Execução de Serviços."
    });
    options.AddSecurityDefinition("Bearer", new()
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Bearer JWT"
    });
    options.AddSecurityRequirement(new()
    {
        {
            new()
            {
                Reference = new()
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AdicionarInfraestrutura(builder.Configuration);
builder.Services.AdicionarAplicacao();
builder.Services.AdicionarAdaptadores();
builder.Services.AdicionarJwtBearer(builder.Configuration);
builder.Services.AdicionarPoliticas();
builder.Services.AdicionarWebhook(builder.Configuration);

// Observabilidade minima: OpenTelemetry expondo /metrics (Prometheus).
// Instrumenta requests do ASP.NET Core + runtime .NET; Serilog segue para stdout.
builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddRuntimeInstrumentation()
        .AddPrometheusExporter());

// Modo Job de migração (Kubernetes): "dotnet Oficina.Api.dll migrate" ou STARTUP_TASK=migrate.
// Aplica migrations + bootstrap do admin e ENCERRA sem subir o servidor web.
// Roda o inicializador diretamente (o HostedService não é iniciado neste caminho,
// pois builder.Build() não dispara hosted services — só app.Run() faria).
var tarefaStartup = Environment.GetEnvironmentVariable("STARTUP_TASK");
if (args.Contains("migrate") ||
    string.Equals(tarefaStartup, "migrate", StringComparison.OrdinalIgnoreCase))
{
    await using var appMigrate = builder.Build();
    var logMigrate = appMigrate.Services
        .GetRequiredService<ILoggerFactory>()
        .CreateLogger("Migrate");
    var inicializador = appMigrate.Services.GetRequiredService<IInicializadorBanco>();

    logMigrate.LogInformation("STARTUP_TASK=migrate — migração + bootstrap; encerrando após concluir.");
    await inicializador.ExecutarAsync(
        appMigrate.Services, appMigrate.Configuration, logMigrate, CancellationToken.None);
    return;
}

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Oficina Mecânica API v1");
    });
}

app.UseMiddleware<MiddlewareDeCorrelacao>();
app.UseSerilogRequestLogging();
app.UseMiddleware<MiddlewareDeExcecoes>();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Endpoint de scraping do Prometheus (anonimo): expõe /metrics.
app.MapPrometheusScrapingEndpoint();

app.Run();

public partial class Program { }
