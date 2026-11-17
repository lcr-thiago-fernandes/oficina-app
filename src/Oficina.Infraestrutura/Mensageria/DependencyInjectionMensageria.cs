using Amazon.SimpleNotificationService;
using Amazon.SQS;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Oficina.Aplicacao.OrdensServico.Saga;
using Oficina.Dominio;
using Oficina.Infraestrutura.Mensageria.Contratos;
using Oficina.Infraestrutura.Mensageria.Observabilidade;
using Oficina.Infraestrutura.Mensageria.Saga;
using Oficina.Infraestrutura.Persistencia;

namespace Oficina.Infraestrutura.Mensageria;

public static class DependencyInjectionMensageria
{
    /// <summary>Produção: registro (saga EF + outbox) e transporte (InMemory ou Amazon SQS/SNS).</summary>
    public static IServiceCollection AdicionarMensageria(this IServiceCollection services, IConfiguration configuration)
    {
        var secao = configuration.GetSection(OpcoesMensageria.Secao);
        var opcoes = secao.Get<OpcoesMensageria>() ?? new OpcoesMensageria();
        // Valida o prefixo já na inicialização: nome inválido não chega à AWS.
        var nomes = new NomesMensageria(opcoes.PrefixoAmbiente);

        services.Configure<OpcoesMensageria>(secao);
        services.AddSingleton(nomes);
        services.TryAddScoped<IAcessorCorrelacao, AcessorCorrelacaoNulo>();
        services.AddScoped<IRegistradorPassoSaga, RegistradorPassoSaga>();
        services.AddScoped<ISagaOrdemServicoGateway, SagaOrdemServicoGateway>();

        services.AddMassTransit(x =>
        {
            ConfigurarRegistro(x, opcoes);
            x.AddDelayedMessageScheduler();

            if (opcoes.Transporte == TransporteMensageria.AmazonSqs)
            {
                x.UsingAmazonSqs((ctx, cfg) =>
                {
                    cfg.Host(opcoes.Regiao, h =>
                    {
                        if (!string.IsNullOrWhiteSpace(opcoes.ServiceUrl))
                        {
                            // Só LocalStack: endpoint local e chaves fictícias. Na AWS vale a cadeia padrão (IRSA).
                            h.Config(new AmazonSQSConfig { ServiceURL = opcoes.ServiceUrl });
                            h.Config(new AmazonSimpleNotificationServiceConfig { ServiceURL = opcoes.ServiceUrl });
                            h.AccessKey("test");
                            h.SecretKey("test");
                        }
                    });
                    // O nome padrão ({máquina}_{assembly}_bus_{id}) daria AccessDenied (IAM só *-oficina-*);
                    // a fila é auto-delete e cada réplica precisa da sua: sufixo com o nome do pod.
                    cfg.OverrideDefaultBusEndpointQueueName(nomes.FilaBus(NomesMensageria.InstanciaAtual()));
                    ConfigurarBus(ctx, cfg, nomes);
                });
            }
            else if (opcoes.Transporte == TransporteMensageria.InMemory)
            {
                // InMemory não tem OverrideDefaultBusEndpointQueueName; sem IAM, o nome é irrelevante.
                x.UsingInMemory((ctx, cfg) => ConfigurarBus(ctx, cfg, nomes));
            }
            else
            {
                throw new InvalidOperationException(
                    $"Valor de {OpcoesMensageria.Secao}:Transporte desconhecido: '{opcoes.Transporte}'.");
            }
        });

        return services;
    }

    /// <summary>
    /// Tudo que é registro e nada de transporte: usado pela produção e pelos testes
    /// (<c>AddMassTransitTestHarness(x =&gt; ConfigurarRegistro(x, opcoes))</c>). O repositório EF é declarado
    /// explicitamente para prevalecer sobre o in-memory que o harness registra como padrão.
    /// </summary>
    public static void ConfigurarRegistro(IBusRegistrationConfigurator x, OpcoesMensageria opcoes)
    {
        var nomes = new NomesMensageria(opcoes.PrefixoAmbiente);

        x.AddEntityFrameworkOutbox<OficinaDbContext>(o =>
        {
            o.UsePostgres();
            o.UseBusOutbox();
            o.QueryDelay = TimeSpan.FromMilliseconds(opcoes.IntervaloOutboxMs);
        });

        x.AddSagaStateMachine<OrdemServicoSaga, EstadoSagaOrdemServico>()
            .EntityFrameworkRepository(r =>
            {
                r.ConcurrencyMode = ConcurrencyMode.Optimistic;
                r.ExistingDbContext<OficinaDbContext>();
                r.UsePostgres();
            })
            .Endpoint(e => e.Name = nomes.FilaSaga);

        // Só vale para endpoints criados por ConfigureEndpoints (produção e harness).
        x.AddConfigureEndpointsCallback((ctx, _, cfg) => ConfigurarEndpoint(ctx, cfg, opcoes));
    }

    /// <summary>Só o que é do bus (usado só na produção; nos testes o bus é o in-memory do harness).</summary>
    public static void ConfigurarBus<TEndpoint>(
        IBusRegistrationContext ctx, IBusFactoryConfigurator<TEndpoint> cfg, NomesMensageria nomes)
        where TEndpoint : IReceiveEndpointConfigurator
    {
        TopologiaSaga.Aplicar(cfg, nomes);
        cfg.ConfigureJsonSerializerOptions(o => OpcoesJsonContrato.Configurar(o));
        // UseDelayedRedelivery exige um scheduler.
        cfg.UseDelayedMessageScheduler();
        cfg.ConfigureEndpoints(ctx);
    }

    public static void ConfigurarEndpoint(IRegistrationContext ctx, IReceiveEndpointConfigurator cfg, OpcoesMensageria opcoes)
    {
        ConfigurarRetentativas(cfg, opcoes);
        // Mais interno: cada tentativa (retry/redelivery) abre escopo, DbContext e transação novos.
        cfg.UseEntityFrameworkOutbox<OficinaDbContext>(ctx);
    }

    /// <summary>
    /// <c>PublishFaults = false</c> (os tópicos <c>Fault&lt;T&gt;</c> teriam o nome do tipo .NET, fora do IAM;
    /// a falha definitiva vai para a fila <c>_error</c>), redelivery atrasado (mais externo) e retry imediato.
    /// O retry cobre toda exceção técnica (inclusive <c>DbUpdateConcurrencyException</c> do <c>xmin</c>);
    /// regra de negócio violada (<see cref="ExcecaoDeDominio"/>) não melhora com retry imediato.
    /// </summary>
    public static void ConfigurarRetentativas(IReceiveEndpointConfigurator cfg, OpcoesMensageria opcoes)
    {
        cfg.PublishFaults = false;
        cfg.UseDelayedRedelivery(r => r.Intervals(IntervalosRedelivery(opcoes)));
        var (limite, minimo, maximo, delta) = ParametrosRetry(opcoes);
        cfg.UseMessageRetry(r =>
        {
            r.Exponential(limite, minimo, maximo, delta);
            r.Ignore<ExcecaoDeDominio>();
        });
    }

    public static (int Limite, TimeSpan Minimo, TimeSpan Maximo, TimeSpan Delta) ParametrosRetry(OpcoesMensageria opcoes) =>
        (3, opcoes.IntervaloMinimoRetry, opcoes.IntervaloMinimoRetry * 25, opcoes.IntervaloMinimoRetry * 2.5);

    public static TimeSpan[] IntervalosRedelivery(OpcoesMensageria opcoes) =>
        new[] { opcoes.IntervaloBaseRedelivery, opcoes.IntervaloBaseRedelivery * 4, opcoes.IntervaloBaseRedelivery * 10 };
}
