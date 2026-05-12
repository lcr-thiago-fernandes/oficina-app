using FluentValidation;
using Oficina.Aplicacao.OrdensServico.Dtos;

namespace Oficina.Aplicacao.OrdensServico.Validacoes;

public class CriarOrdemValidator : AbstractValidator<CriarOrdemRequest>
{
    public CriarOrdemValidator()
    {
        RuleFor(x => x.ClienteId).NotEqual(Guid.Empty);
        RuleFor(x => x.VeiculoId).NotEqual(Guid.Empty);
        RuleFor(x => x.Observacoes).MaximumLength(2000);
    }
}
