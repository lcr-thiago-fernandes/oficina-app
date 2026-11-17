using System.Text.RegularExpressions;

namespace Oficina.Infraestrutura.Mensageria;

/// <summary>
/// Nomes de filas e tópicos da saga, todos com o prefixo do ambiente e casando com a política
/// IAM <c>*-oficina-*</c>.
/// </summary>
public sealed partial class NomesMensageria
{
    /// <summary>Limite do nome de fila no SQS.</summary>
    public const int TamanhoMaximoFilaSqs = 80;

    private readonly string _prefixo;

    public NomesMensageria(string prefixo)
    {
        // \z (e não $): "hml\n" não pode passar — o nome iria com a quebra de linha para a AWS.
        if (prefixo is null || !PrefixoValido().IsMatch(prefixo))
            throw new ArgumentException(
                $"Valor de {OpcoesMensageria.Secao}:PrefixoAmbiente inválido: '{prefixo}'. " +
                "Use só letras minúsculas, dígitos e '-' (ex.: local, hml, prd).",
                nameof(prefixo));
        _prefixo = prefixo;
    }

    public string Prefixo => _prefixo;
    public string FilaComandosBilling => $"{_prefixo}-oficina-billing-comandos";
    public string FilaComandosExecucao => $"{_prefixo}-oficina-execucao-comandos";
    public string FilaSaga => $"{_prefixo}-oficina-os-saga";

    /// <summary>
    /// Fila padrão do bus (temporária, auto-delete), uma por réplica: sufixo com a instância (nome do pod) em
    /// minúsculas, caracteres fora de <c>[a-z0-9-_]</c> trocados por '-', nome inteiro truncado no limite do SQS.
    /// </summary>
    public string FilaBus(string instancia)
    {
        var nome = $"{_prefixo}-oficina-os-bus-{CaractereInvalidoNaFila().Replace(instancia.ToLowerInvariant(), "-")}";
        return nome.Length > TamanhoMaximoFilaSqs ? nome[..TamanhoMaximoFilaSqs] : nome;
    }

    /// <summary>HOSTNAME (nome do pod no Kubernetes); sem ele, o nome da máquina.</summary>
    public static string InstanciaAtual() =>
        Environment.GetEnvironmentVariable("HOSTNAME") is { } hostname && !string.IsNullOrWhiteSpace(hostname)
            ? hostname
            : Environment.MachineName;

    /// <summary>Tópico de um dos 16 contratos da saga.</summary>
    public string NomeTopico(string nome) => $"{_prefixo}-oficina-saga-{nome}";

    /// <summary>Tópico de um evento interno do OS Service.</summary>
    public string NomeTopicoInterno(string nome) => $"{_prefixo}-oficina-os-{nome}";

    public Uri EnderecoComandosBilling => new($"queue:{FilaComandosBilling}");
    public Uri EnderecoComandosExecucao => new($"queue:{FilaComandosExecucao}");

    [GeneratedRegex(@"^[a-z0-9-]+\z")]
    private static partial Regex PrefixoValido();

    [GeneratedRegex("[^a-z0-9_-]")]
    private static partial Regex CaractereInvalidoNaFila();
}
