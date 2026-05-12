namespace Oficina.Aplicacao.Auth;

public sealed record LoginResponse(string AccessToken, int ExpiresInSeconds, bool PrecisaTrocarSenha);
