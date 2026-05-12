using FluentValidation;
using Oficina.Aplicacao.OrdensServico.Dtos;

namespace Oficina.Aplicacao.OrdensServico.Validacoes;

public class AdicionarItemPecaValidator : AbstractValidator<AdicionarItemPecaRequest>
{
    public AdicionarItemPecaValidator()
    {
        RuleFor(x => x.PecaId).NotEqual(Guid.Empty);
        RuleFor(x => x.Quantidade).GreaterThan(0);
    }
}
