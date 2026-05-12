namespace Oficina.Dominio.Estoque;

public sealed class Sku : IEquatable<Sku>
{
    public string Valor { get; }

    private Sku(string valor) => Valor = valor;

    public static Sku Criar(string entrada)
    {
        if (string.IsNullOrWhiteSpace(entrada))
            throw new SkuInvalidoException("SKU não pode ser vazio.");

        var normalizado = entrada.Trim().ToUpperInvariant();
        if (normalizado.Length < 3 || normalizado.Length > 50)
            throw new SkuInvalidoException("SKU deve ter entre 3 e 50 caracteres.");

        return new Sku(normalizado);
    }

    public bool Equals(Sku? other) => other is not null && Valor == other.Valor;
    public override bool Equals(object? obj) => Equals(obj as Sku);
    public override int GetHashCode() => Valor.GetHashCode();
    public override string ToString() => Valor;
}
