using FluentValidation;
using Oficina.Aplicacao.Catalogo.Dtos;

namespace Oficina.Aplicacao.Catalogo.Validacoes;

public class AtualizarServicoValidator : AbstractValidator<AtualizarServicoRequest>
{
    public AtualizarServicoValidator()
    {
        RuleFor(x => x.Nome).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Descricao).MaximumLength(2000);
        RuleFor(x => x.PrecoBase).GreaterThan(0);
        RuleFor(x => x.TempoEstimadoMinutos).GreaterThan(0);
    }
}
