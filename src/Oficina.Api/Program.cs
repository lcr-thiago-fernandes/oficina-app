using FluentValidation.AspNetCore;
using Oficina.Aplicacao;
using Oficina.Api.Configuracao;
using Oficina.Infraestrutura;
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
builder.Services.AdicionarJwtBearer(builder.Configuration);
builder.Services.AdicionarPoliticas();
builder.Services.AdicionarRateLimit();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Oficina Mecânica API v1");
    });
}

app.UseSerilogRequestLogging();
app.UseMiddleware<MiddlewareDeExcecoes>();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapControllers();

app.Run();

public partial class Program { }
