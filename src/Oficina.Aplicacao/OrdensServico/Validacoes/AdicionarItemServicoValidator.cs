using FluentValidation;
using Oficina.Aplicacao.OrdensServico.Dtos;

namespace Oficina.Aplicacao.OrdensServico.Validacoes;

public class AdicionarItemServicoValidator : AbstractValidator<AdicionarItemServicoRequest>
{
    public AdicionarItemServicoValidator()
    {
        RuleFor(x => x.ServicoId).NotEqual(Guid.Empty);
        RuleFor(x => x.Quantidade).GreaterThan(0);
    }
}
