namespace Oficina.Infraestrutura.Auth;

public class JwtOptions
{
    public const string Secao = "Jwt";

    public string Secret { get; set; } = string.Empty;
    public string Issuer { get; set; } = "oficina-api";
    public string Audience { get; set; } = "oficina-clients";
    public int ExpiracaoEmMinutos { get; set; } = 60;
}
