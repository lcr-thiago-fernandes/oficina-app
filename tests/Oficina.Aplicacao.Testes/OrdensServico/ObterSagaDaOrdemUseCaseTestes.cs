using FluentAssertions;
using Moq;
using Oficina.Aplicacao.OrdensServico;
using Oficina.Aplicacao.OrdensServico.Saga;
using Oficina.Aplicacao.OrdensServico.Saga.Dtos;
using Xunit;

namespace Oficina.Aplicacao.Testes.OrdensServico;

public class ObterSagaDaOrdemUseCaseTestes
{
    [Fact]
    public async Task Obter_DelegaAoGateway()
    {
        var id = Guid.NewGuid();
        var estado = new EstadoSagaResponse(id, 7, "AguardandoAprovacao", null, null,
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, Array.Empty<PassoSagaResponse>());
        var saga = new Mock<ISagaOrdemServicoGateway>();
        saga.Setup(s => s.ObterEstadoAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(estado);

        var resp = await new ObterSagaDaOrdemUseCase(saga.Object).ExecutarAsync(id, default);

        resp.Should().BeSameAs(estado);
    }
}
