using MassTransit;
using MassTransit.DependencyInjection;
using MassTransit.Middleware.Outbox;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Oficina.Infraestrutura.Mensageria;

namespace Oficina.Testes.Compartilhado;

/// <summary>
/// Troca o bus de produção (registrado por <c>AdicionarMensageria</c>) pelo in-memory do test harness, com o
/// mesmo registro (<see cref="DependencyInjectionMensageria.ConfigurarRegistro"/>: saga EF, outbox, retry).
/// Usado pela Api dos testes de integração e pelo BDD.
/// </summary>
public static class HarnessMensageria
{
    /// <param name="opcoes">Valem no endpoint da saga (retry, redelivery, intervalo do outbox). O prefixo das
    /// filas continua vindo da configuração da Api (a saga lê <c>IOptions&lt;OpcoesMensageria&gt;</c>).</param>
    public static IServiceCollection SubstituirMensageriaPeloHarness(
        this IServiceCollection services, OpcoesMensageria opcoes, Action<IBusRegistrationConfigurator>? extra = null)
    {
        // O AddMassTransitTestHarness desfaz o AddMassTransit anterior (RemoveMassTransit, interno), mas no 8.4.1
        // deixa para trás os callbacks de endpoint e a notificação do bus outbox. Sem esta limpeza, outbox, retry
        // e redelivery seriam aplicados duas vezes (o primeiro com as opções de produção) e o serviço de entrega
        // do bus outbox falharia em loop com um IBusOutboxNotification que o bus nunca inicializou.
        services.RemoveAll<Bind<IBus, IConfigureReceiveEndpoint>>();
        services.RemoveAll<IBusOutboxNotification>();

        return services.AddMassTransitTestHarness(x =>
        {
            // A fixture vive a coleção inteira: o timeout do harness não pode vencer no meio dela.
            x.SetTestTimeouts(testTimeout: TimeSpan.FromMinutes(30));
            DependencyInjectionMensageria.ConfigurarRegistro(x, opcoes);
            extra?.Invoke(x);
        });
    }
}
