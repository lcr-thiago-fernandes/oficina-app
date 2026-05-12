using System.Text.RegularExpressions;

namespace Oficina.Dominio.Clientes;

public sealed class Telefone : IEquatable<Telefone>
{
    public string Valor { get; }

    private Telefone(string valor) => Valor = valor;

    public static Telefone Criar(string entrada)
    {
        if (string.IsNullOrWhiteSpace(entrada))
            throw new ArgumentException("Telefone não pode ser vazio.", nameof(entrada));

        var digitos = Regex.Replace(entrada, @"\D", "");
        if (digitos.Length is < 10 or > 11)
            throw new ArgumentException(
                "Telefone deve ter 10 ou 11 dígitos (com DDD).", nameof(entrada));

        return new Telefone(digitos);
    }

    public bool Equals(Telefone? other) => other is not null && Valor == other.Valor;
    public override bool Equals(object? obj) => Equals(obj as Telefone);
    public override int GetHashCode() => Valor.GetHashCode();
    public override string ToString() => Valor;
}
