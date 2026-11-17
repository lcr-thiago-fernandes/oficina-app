using System.Text.RegularExpressions;

namespace Oficina.Infraestrutura.Mensageria;

/// <summary>
/// Nomes de filas e tópicos da saga, todos com o prefixo do ambiente e casando com a política
/// IAM <c>*-oficina-*</c>.
/// </summary>
public sealed partial class NomesMensageria
{
    private readonly string _prefixo;

    public NomesMensageria(string prefixo)
    {
        if (prefixo is null || !PrefixoValido().IsMatch(prefixo))
            throw new ArgumentException(
                $"Prefixo de ambiente inválido: '{prefixo}'. Use só letras minúsculas e dígitos (ex.: local, hml, prd).",
                nameof(prefixo));
        _prefixo = prefixo;
    }

    public string FilaComandosBilling => $"{_prefixo}-oficina-billing-comandos";
    public string FilaComandosExecucao => $"{_prefixo}-oficina-execucao-comandos";
    public string FilaSaga => $"{_prefixo}-oficina-os-saga";
    public string FilaBus => $"{_prefixo}-oficina-os-bus";

    /// <summary>Tópico de um dos 16 contratos da saga.</summary>
    public string NomeTopico(string nome) => $"{_prefixo}-oficina-saga-{nome}";

    /// <summary>Tópico de um evento interno do OS Service.</summary>
    public string NomeTopicoInterno(string nome) => $"{_prefixo}-oficina-os-{nome}";

    public Uri EnderecoComandosBilling => new($"queue:{FilaComandosBilling}");
    public Uri EnderecoComandosExecucao => new($"queue:{FilaComandosExecucao}");

    [GeneratedRegex("^[a-z0-9]+$")]
    private static partial Regex PrefixoValido();
}
