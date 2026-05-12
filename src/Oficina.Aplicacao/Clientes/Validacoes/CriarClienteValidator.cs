using FluentValidation;
using Oficina.Aplicacao.Clientes.Dtos;

namespace Oficina.Aplicacao.Clientes.Validacoes;

public class CriarClienteValidator : AbstractValidator<CriarClienteRequest>
{
    public CriarClienteValidator()
    {
        RuleFor(x => x.Nome).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Documento).NotEmpty();
        RuleFor(x => x.Email).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Telefone).NotEmpty().MaximumLength(20);
    }
}
