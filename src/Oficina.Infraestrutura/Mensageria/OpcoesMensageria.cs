namespace Oficina.Infraestrutura.Mensageria;

public enum TransporteMensageria
{
    InMemory,
    AmazonSqs
}

/// <summary>Seção <c>Mensageria</c> da configuração (variáveis <c>Mensageria__*</c>).</summary>
public class OpcoesMensageria
{
    public const string Secao = "Mensageria";

    public TransporteMensageria Transporte { get; set; } = TransporteMensageria.InMemory;

    /// <summary>Prefixo do ambiente (<c>local</c>, <c>hml</c>, <c>prd</c>) de filas e tópicos.</summary>
    public string PrefixoAmbiente { get; set; } = "local";

    public string Regiao { get; set; } = "us-east-1";

    /// <summary>Endpoint alternativo do SQS/SNS (LocalStack). Nulo → AWS real.</summary>
    public string? ServiceUrl { get; set; }

    public int IntervaloOutboxMs { get; set; } = 1000;

    /// <summary>
    /// Escala do retry imediato: exponencial de 3 tentativas entre este valor e 25×, passo de 2,5×
    /// (padrão 200 ms → 200 ms, 5 s, 500 ms, das Global Constraints). Só os testes mudam.
    /// </summary>
    public TimeSpan IntervaloMinimoRetry { get; set; } = TimeSpan.FromMilliseconds(200);

    /// <summary>
    /// Escala do redelivery atrasado: base, 4× e 10× (padrão 30 s → 30 s, 2 min, 5 min, das Global
    /// Constraints). Só os testes mudam.
    /// </summary>
    public TimeSpan IntervaloBaseRedelivery { get; set; } = TimeSpan.FromSeconds(30);
}
