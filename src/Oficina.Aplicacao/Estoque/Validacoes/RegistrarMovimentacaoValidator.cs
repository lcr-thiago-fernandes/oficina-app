using FluentValidation;
using Oficina.Aplicacao.Estoque.Dtos;

namespace Oficina.Aplicacao.Estoque.Validacoes;

public class RegistrarMovimentacaoValidator : AbstractValidator<RegistrarMovimentacaoRequest>
{
    public RegistrarMovimentacaoValidator()
    {
        RuleFor(x => x.Tipo).NotEmpty()
            .Must(t => t is "Entrada" or "Saida")
            .WithMessage("Tipo deve ser 'Entrada' ou 'Saida'.");
        RuleFor(x => x.Quantidade).GreaterThan(0);
        RuleFor(x => x.Motivo).NotEmpty().MaximumLength(100);
    }
}
