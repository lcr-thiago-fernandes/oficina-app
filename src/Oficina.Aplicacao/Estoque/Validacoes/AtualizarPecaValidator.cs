using FluentValidation;
using Oficina.Aplicacao.Estoque.Dtos;

namespace Oficina.Aplicacao.Estoque.Validacoes;

public class AtualizarPecaValidator : AbstractValidator<AtualizarPecaRequest>
{
    public AtualizarPecaValidator()
    {
        RuleFor(x => x.Nome).NotEmpty().MaximumLength(120);
        RuleFor(x => x.PrecoUnitario).GreaterThan(0);
    }
}
