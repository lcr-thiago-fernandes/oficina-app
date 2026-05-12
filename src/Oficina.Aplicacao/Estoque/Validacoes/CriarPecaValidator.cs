using FluentValidation;
using Oficina.Aplicacao.Estoque.Dtos;

namespace Oficina.Aplicacao.Estoque.Validacoes;

public class CriarPecaValidator : AbstractValidator<CriarPecaRequest>
{
    public CriarPecaValidator()
    {
        RuleFor(x => x.Sku).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Nome).NotEmpty().MaximumLength(120);
        RuleFor(x => x.PrecoUnitario).GreaterThan(0);
    }
}
