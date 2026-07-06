using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Oficina.Infraestrutura.Auth;
using Oficina.Infraestrutura.Persistencia;

namespace Oficina.Infraestrutura.Testes.Auth;

public class BootstrapAdminHostedServiceTestes
{
    private static IConfiguration Config(string? executarNoStartup)
    {
        var dados = new Dictionary<string, string?>();
        if (executarNoStartup is not null)
            dados["Bootstrap:ExecutarNoStartup"] = executarNoStartup;

        return new ConfigurationBuilder().AddInMemoryCollection(dados).Build();
    }

    private static BootstrapAdminHostedService Criar(
        IConfiguration config,
        Mock<IInicializadorBanco> inicializador)
    {
        // O IServiceProvider não é tocado no caminho "flag=false" e é ignorado
        // pelo duplo no caminho "flag=true", então um provider mínimo basta.
        var sp = Mock.Of<IServiceProvider>();
        return new BootstrapAdminHostedService(
            sp,
            config,
            NullLogger<BootstrapAdminHostedService>.Instance,
            inicializador.Object);
    }

    [Fact]
    public async Task ExecutarNoStartupFalse_NaoChamaOInicializador()
    {
        var inicializador = new Mock<IInicializadorBanco>();
        var svc = Criar(Config("false"), inicializador);

        await svc.StartAsync(CancellationToken.None);

        inicializador.Verify(
            i => i.ExecutarAsync(It.IsAny<IServiceProvider>(), It.IsAny<IConfiguration>(),
                It.IsAny<Microsoft.Extensions.Logging.ILogger>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SemAChave_Default_ChamaOInicializador()
    {
        var inicializador = new Mock<IInicializadorBanco>();
        var svc = Criar(Config(null), inicializador);

        await svc.StartAsync(CancellationToken.None);

        inicializador.Verify(
            i => i.ExecutarAsync(It.IsAny<IServiceProvider>(), It.IsAny<IConfiguration>(),
                It.IsAny<Microsoft.Extensions.Logging.ILogger>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecutarNoStartupTrue_ChamaOInicializador()
    {
        var inicializador = new Mock<IInicializadorBanco>();
        var svc = Criar(Config("true"), inicializador);

        await svc.StartAsync(CancellationToken.None);

        inicializador.Verify(
            i => i.ExecutarAsync(It.IsAny<IServiceProvider>(), It.IsAny<IConfiguration>(),
                It.IsAny<Microsoft.Extensions.Logging.ILogger>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
