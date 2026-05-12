namespace Oficina.Dominio.Auth;

public sealed class Senha
{
    private const int BCryptCost = 12;
    private const int TamanhoMinimo = 8;

    public string Hash { get; }

    private Senha(string hash)
    {
        Hash = hash;
    }

    public static Senha DeTextoPuro(string textoPuro)
    {
        if (string.IsNullOrWhiteSpace(textoPuro))
            throw new SenhaInvalidaException("Senha não pode ser vazia.");

        if (textoPuro.Length < TamanhoMinimo)
            throw new SenhaInvalidaException(
                $"Senha precisa ter ao menos {TamanhoMinimo} caracteres.");

        var hash = BCrypt.Net.BCrypt.HashPassword(textoPuro, BCryptCost);
        return new Senha(hash);
    }

    public static Senha DeHashExistente(string hash)
    {
        if (string.IsNullOrWhiteSpace(hash))
            throw new SenhaInvalidaException("Hash não pode ser vazio.");
        return new Senha(hash);
    }

    public bool Verificar(string textoPuro)
    {
        if (string.IsNullOrEmpty(textoPuro))
            return false;
        return BCrypt.Net.BCrypt.Verify(textoPuro, Hash);
    }
}
