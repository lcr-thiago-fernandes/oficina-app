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
}
