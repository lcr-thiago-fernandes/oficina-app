using Oficina.Dominio.OrdensServico;

namespace Oficina.Aplicacao.OrdensServico.Gateways;

// Porta de saída (Clean Architecture): notifica o cliente sobre mudanças de
// status da OS. A implementação concreta (e-mail, SMS, etc.) vive em
// Oficina.Infraestrutura. No MVP é um mock que apenas registra em log.
public interface INotificacaoGateway
{
    Task NotificarMudancaDeStatusAsync(OrdemDeServico ordem, CancellationToken ct);
}
