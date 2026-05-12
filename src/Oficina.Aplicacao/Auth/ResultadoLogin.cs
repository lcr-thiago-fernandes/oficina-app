namespace Oficina.Aplicacao.Auth;

public abstract record ResultadoLogin
{
    public sealed record Sucesso(LoginResponse Response) : ResultadoLogin;
    public sealed record CredenciaisInvalidas() : ResultadoLogin;
    public sealed record UsuarioInativo() : ResultadoLogin;
}
