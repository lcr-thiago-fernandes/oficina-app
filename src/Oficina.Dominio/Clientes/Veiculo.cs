namespace Oficina.Dominio.Clientes;

public sealed class Veiculo
{
    private const int AnoMinimo = 1900;

    public Guid Id { get; private set; }
    public Placa Placa { get; private set; } = null!;
    public string Marca { get; private set; } = null!;
    public string Modelo { get; private set; } = null!;
    public int Ano { get; private set; }
    public DateTimeOffset CriadoEm { get; private set; }

    private Veiculo() { }

    public static Veiculo Criar(Placa placa, string marca, string modelo, int ano)
    {
        ValidarTexto(marca, nameof(marca));
        ValidarTexto(modelo, nameof(modelo));
        ValidarAno(ano);

        return new Veiculo
        {
            Id = Guid.NewGuid(),
            Placa = placa,
            Marca = marca.Trim(),
            Modelo = modelo.Trim(),
            Ano = ano,
            CriadoEm = DateTimeOffset.UtcNow
        };
    }

    public void AtualizarDados(string marca, string modelo, int ano)
    {
        ValidarTexto(marca, nameof(marca));
        ValidarTexto(modelo, nameof(modelo));
        ValidarAno(ano);

        Marca = marca.Trim();
        Modelo = modelo.Trim();
        Ano = ano;
    }

    private static void ValidarTexto(string valor, string nomeCampo)
    {
        if (string.IsNullOrWhiteSpace(valor))
            throw new ArgumentException($"O campo '{nomeCampo}' é obrigatório.", nomeCampo);
    }

    private static void ValidarAno(int ano)
    {
        var anoMaximo = DateTime.UtcNow.Year + 1;
        if (ano < AnoMinimo || ano > anoMaximo)
            throw new ArgumentException(
                $"O ano deve estar entre {AnoMinimo} e {anoMaximo}.", nameof(ano));
    }
}
