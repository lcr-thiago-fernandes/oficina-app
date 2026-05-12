using System.Text.RegularExpressions;

namespace Oficina.Dominio.Clientes;

public sealed class Placa : IEquatable<Placa>
{
    private static readonly Regex Antigo = new("^[A-Z]{3}[0-9]{4}$", RegexOptions.Compiled);
    private static readonly Regex Mercosul = new("^[A-Z]{3}[0-9][A-Z][0-9]{2}$", RegexOptions.Compiled);

    public string Valor { get; }

    private Placa(string valor) => Valor = valor;

    public static Placa Criar(string entrada)
    {
        if (string.IsNullOrWhiteSpace(entrada))
            throw new PlacaInvalidaException("Placa não pode ser vazia.");

        var normalizado = entrada.Replace("-", "").Replace(" ", "").ToUpperInvariant();

        if (!Antigo.IsMatch(normalizado) && !Mercosul.IsMatch(normalizado))
            throw new PlacaInvalidaException(
                $"Placa '{entrada}' não está em formato antigo (AAA9999) nem Mercosul (AAA9A99).");

        return new Placa(normalizado);
    }

    public bool Equals(Placa? other) => other is not null && Valor == other.Valor;
    public override bool Equals(object? obj) => Equals(obj as Placa);
    public override int GetHashCode() => Valor.GetHashCode();
    public override string ToString() => Valor;
}
