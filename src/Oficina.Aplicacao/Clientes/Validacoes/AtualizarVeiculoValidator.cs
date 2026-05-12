using FluentValidation;
using Oficina.Aplicacao.Clientes.Dtos;

namespace Oficina.Aplicacao.Clientes.Validacoes;

public class AtualizarVeiculoValidator : AbstractValidator<AtualizarVeiculoRequest>
{
    public AtualizarVeiculoValidator()
    {
        RuleFor(x => x.Marca).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Modelo).NotEmpty().MaximumLength(80);
        RuleFor(x => x.Ano).GreaterThanOrEqualTo(1900);
    }
}
