namespace Oficina.Dominio.Clientes;

public sealed class Cliente
{
    private readonly List<Veiculo> _veiculos = new();

    public Guid Id { get; private set; }
    public string Nome { get; private set; } = null!;
    public Documento Documento { get; private set; } = null!;
    public Email Email { get; private set; } = null!;
    public Telefone Telefone { get; private set; } = null!;
    public bool Ativo { get; private set; }
    public DateTimeOffset CriadoEm { get; private set; }
    public DateTimeOffset AtualizadoEm { get; private set; }

    public IReadOnlyCollection<Veiculo> Veiculos => _veiculos.AsReadOnly();

    private Cliente() { }

    public static Cliente Criar(string nome, Documento documento, Email email, Telefone telefone)
    {
        if (string.IsNullOrWhiteSpace(nome))
            throw new ClienteInvalidoException("O nome do cliente é obrigatório.");

        var agora = DateTimeOffset.UtcNow;
        return new Cliente
        {
            Id = Guid.NewGuid(),
            Nome = nome.Trim(),
            Documento = documento,
            Email = email,
            Telefone = telefone,
            Ativo = true,
            CriadoEm = agora,
            AtualizadoEm = agora
        };
    }

    public void AtualizarContato(string nome, Email email, Telefone telefone)
    {
        if (string.IsNullOrWhiteSpace(nome))
            throw new ClienteInvalidoException("O nome do cliente é obrigatório.");

        Nome = nome.Trim();
        Email = email;
        Telefone = telefone;
        AtualizadoEm = DateTimeOffset.UtcNow;
    }

    public void Inativar()
    {
        Ativo = false;
        AtualizadoEm = DateTimeOffset.UtcNow;
    }

    public void Ativar()
    {
        Ativo = true;
        AtualizadoEm = DateTimeOffset.UtcNow;
    }

    public void AdicionarVeiculo(Veiculo veiculo)
    {
        if (_veiculos.Any(v => v.Placa.Equals(veiculo.Placa)))
            throw new PlacaJaCadastradaException(veiculo.Placa.Valor);

        _veiculos.Add(veiculo);
        AtualizadoEm = DateTimeOffset.UtcNow;
    }

    public void RemoverVeiculo(Placa placa)
    {
        var v = _veiculos.FirstOrDefault(x => x.Placa.Equals(placa))
            ?? throw new VeiculoNaoEncontradoException(placa.Valor);
        _veiculos.Remove(v);
        AtualizadoEm = DateTimeOffset.UtcNow;
    }

    public void AtualizarVeiculo(Placa placa, string marca, string modelo, int ano)
    {
        var v = _veiculos.FirstOrDefault(x => x.Placa.Equals(placa))
            ?? throw new VeiculoNaoEncontradoException(placa.Valor);

        v.AtualizarDados(marca, modelo, ano);
        AtualizadoEm = DateTimeOffset.UtcNow;
    }
}
