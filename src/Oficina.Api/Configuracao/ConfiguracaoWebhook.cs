namespace Oficina.Api.Configuracao;

public class WebhookOptions
{
    public const string Secao = "Webhook";
    public string? Token { get; set; }
}

public static class ConfiguracaoWebhook
{
    public static IServiceCollection AdicionarWebhook(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<WebhookOptions>(configuration.GetSection(WebhookOptions.Secao));
        services.AddScoped<ValidacaoTokenWebhookFilter>();
        return services;
    }
}
