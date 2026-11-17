namespace Oficina.Infraestrutura.Mensageria.Observabilidade;

/// <summary>Correlation id da requisição atual, propagado no header <c>X-Correlation-Id</c> das mensagens.</summary>
public interface IAcessorCorrelacao
{
    string? Atual { get; }
}

/// <summary>Padrão fora de uma requisição HTTP: sem correlation id.</summary>
public sealed class AcessorCorrelacaoNulo : IAcessorCorrelacao
{
    public string? Atual => null;
}
