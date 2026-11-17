using MassTransit;

namespace Oficina.Infraestrutura.Mensageria.Contratos;

/// <summary>
/// Campos comuns de toda mensagem da saga. Excluida da topologia e dos tipos implementados:
/// sem isso o MassTransit publicaria tambem num topico da interface, fora da politica IAM.
/// </summary>
[ExcludeFromTopology, ExcludeFromImplementedTypes]
public interface IMensagemDaSaga
{
    Guid OrdemServicoId { get; }
    DateTime OcorridoEm { get; }
}
