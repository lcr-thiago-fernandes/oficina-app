namespace Oficina.Infraestrutura.Auth;

/// <summary>
/// Parametros de VALIDACAO do token. A API nao emite JWT: quem emite e a funcao
/// serverless oficina-auth, que autentica o cliente pelo CPF. Os defaults abaixo
/// refletem o contrato compartilhado (iss=oficina-auth, aud=oficina-api) e devem
/// bater com k8s/configmap.yaml.
/// </summary>
public class JwtOptions
{
    public const string Secao = "Jwt";

    public string Secret { get; set; } = string.Empty;
    public string Issuer { get; set; } = "oficina-auth";
    public string Audience { get; set; } = "oficina-api";
}
