namespace Oficina.Dominio.Catalogo;

public sealed class Servico
{
    public Guid Id { get; private set; }
    public string Nome { get; private set; } = null!;
    public string Descricao { get; private set; } = null!;
    public decimal PrecoBase { get; private set; }
    public int TempoEstimadoMinutos { get; private set; }
    public bool Ativo { get; private set; }
    public DateTimeOffset CriadoEm { get; private set; }

    private Servico() { }

    public static Servico Criar(string nome, string descricao, decimal precoBase, int tempoEstimadoMinutos)
    {
        Validar(nome, precoBase, tempoEstimadoMinutos);

        return new Servico
        {
            Id = Guid.NewGuid(),
            Nome = nome.Trim(),
            Descricao = (descricao ?? string.Empty).Trim(),
            PrecoBase = precoBase,
            TempoEstimadoMinutos = tempoEstimadoMinutos,
            Ativo = true,
            CriadoEm = DateTimeOffset.UtcNow
        };
    }

    public void AtualizarDados(string nome, string descricao, decimal precoBase, int tempoEstimadoMinutos)
    {
        Validar(nome, precoBase, tempoEstimadoMinutos);

        Nome = nome.Trim();
        Descricao = (descricao ?? string.Empty).Trim();
        PrecoBase = precoBase;
        TempoEstimadoMinutos = tempoEstimadoMinutos;
    }

    public void Inativar() => Ativo = false;
    public void Ativar() => Ativo = true;

    private static void Validar(string nome, decimal precoBase, int tempoEstimadoMinutos)
    {
        if (string.IsNullOrWhiteSpace(nome))
            throw new ServicoInvalidoException("O nome do serviço é obrigatório.");

        if (precoBase <= 0m)
            throw new ServicoInvalidoException("O preço base deve ser maior que zero.");

        if (tempoEstimadoMinutos <= 0)
            throw new ServicoInvalidoException("O tempo estimado em minutos deve ser maior que zero.");
    }
}
