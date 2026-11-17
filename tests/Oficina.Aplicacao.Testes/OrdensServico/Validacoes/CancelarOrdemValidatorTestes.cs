using FluentAssertions;
using Oficina.Aplicacao.OrdensServico.Dtos;
using Oficina.Aplicacao.OrdensServico.Validacoes;
using Xunit;

namespace Oficina.Aplicacao.Testes.OrdensServico.Validacoes;

public class CancelarOrdemValidatorTestes
{
    private readonly CancelarOrdemValidator _validator = new();

    [Theory]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("Cliente desistiu", true)]
    public void Validar_Motivo(string motivo, bool valido) =>
        _validator.Validate(new CancelarOrdemRequest(motivo)).IsValid.Should().Be(valido);

    [Fact]
    public void Validar_MotivoAcimaDe500_Invalido() =>
        _validator.Validate(new CancelarOrdemRequest(new string('x', 501))).IsValid.Should().BeFalse();
}
