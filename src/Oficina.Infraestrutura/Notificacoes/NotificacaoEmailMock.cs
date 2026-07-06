using Microsoft.Extensions.Logging;
using Oficina.Aplicacao.OrdensServico.Gateways;
using Oficina.Dominio.OrdensServico;

namespace Oficina.Infraestrutura.Notificacoes;

// Mock de notificação por e-mail: no MVP apenas registra em log estruturado.
// Substituível por uma impl real (SMTP/provedor) sem tocar nos casos de uso.
public class NotificacaoEmailMock : INotificacaoGateway
{
    private readonly ILogger<NotificacaoEmailMock> _log;

    public NotificacaoEmailMock(ILogger<NotificacaoEmailMock> log) => _log = log;

    public Task NotificarMudancaDeStatusAsync(OrdemDeServico ordem, CancellationToken ct)
    {
        _log.LogInformation(
            "[e-mail] OS #{Numero} agora esta {Status} (cliente {ClienteId})",
            ordem.Numero, ordem.Status, ordem.ClienteId);
        return Task.CompletedTask;
    }
}
