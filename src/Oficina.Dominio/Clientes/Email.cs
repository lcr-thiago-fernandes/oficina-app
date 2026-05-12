using System.Text.RegularExpressions;

namespace Oficina.Dominio.Clientes;

public sealed class Email : IEquatable<Email>
{
    private static readonly Regex Padrao = new(
        @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public string Valor { get; }

    private Email(string valor) => Valor = valor;

    public static Email Criar(string entrada)
    {
        if (string.IsNullOrWhiteSpace(entrada))
            throw new EmailInvalidoException("E-mail não pode ser vazio.");

        var normalizado = entrada.Trim().ToLowerInvariant();
        if (!Padrao.IsMatch(normalizado))
            throw new EmailInvalidoException($"'{entrada}' não é um e-mail válido.");

        return new Email(normalizado);
    }

    public bool Equals(Email? other) => other is not null && Valor == other.Valor;
    public override bool Equals(object? obj) => Equals(obj as Email);
    public override int GetHashCode() => Valor.GetHashCode();
    public override string ToString() => Valor;
}
