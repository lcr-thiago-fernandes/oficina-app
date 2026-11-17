using FluentValidation;
using Oficina.Aplicacao.OrdensServico.Dtos;

namespace Oficina.Aplicacao.OrdensServico.Validacoes;

public class CancelarOrdemValidator : AbstractValidator<CancelarOrdemRequest>
{
    public CancelarOrdemValidator()
    {
        RuleFor(x => x.Motivo).NotEmpty().MaximumLength(500);
    }
}
