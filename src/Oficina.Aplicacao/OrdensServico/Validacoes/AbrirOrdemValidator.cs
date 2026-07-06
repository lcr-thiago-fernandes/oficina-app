using FluentValidation;
using Oficina.Aplicacao.OrdensServico.Dtos;

namespace Oficina.Aplicacao.OrdensServico.Validacoes;

public class AbrirOrdemValidator : AbstractValidator<AbrirOrdemRequest>
{
    public AbrirOrdemValidator()
    {
        RuleFor(x => x.ClienteDados).NotNull().SetValidator(new ClienteDadosValidator());
        RuleFor(x => x.VeiculoDados).NotNull().SetValidator(new VeiculoDadosValidator());
        RuleFor(x => x.Observacoes).MaximumLength(2000);

        RuleForEach(x => x.Servicos).SetValidator(new ItemServicoDtoValidator());
        RuleForEach(x => x.Pecas).SetValidator(new ItemPecaDtoValidator());

        // Uma OS aberta deve ter pelo menos um item (serviço ou peça).
        RuleFor(x => x)
            .Must(x => (x.Servicos?.Count ?? 0) + (x.Pecas?.Count ?? 0) > 0)
            .WithMessage("Informe ao menos um serviço ou peça para abrir a ordem.");
    }
}

public class ClienteDadosValidator : AbstractValidator<ClienteDadosDto>
{
    public ClienteDadosValidator()
    {
        RuleFor(x => x.Documento).NotEmpty();
        RuleFor(x => x.Nome).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Email).NotEmpty();
        RuleFor(x => x.Telefone).NotEmpty();
    }
}

public class VeiculoDadosValidator : AbstractValidator<VeiculoDadosDto>
{
    public VeiculoDadosValidator()
    {
        RuleFor(x => x.Placa).NotEmpty();
        RuleFor(x => x.Marca).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Modelo).NotEmpty().MaximumLength(80);
        RuleFor(x => x.Ano).GreaterThanOrEqualTo(1900);
    }
}

public class ItemServicoDtoValidator : AbstractValidator<ItemServicoDto>
{
    public ItemServicoDtoValidator()
    {
        RuleFor(x => x.ServicoId).NotEqual(Guid.Empty);
        RuleFor(x => x.Quantidade).GreaterThan(0);
    }
}

public class ItemPecaDtoValidator : AbstractValidator<ItemPecaDto>
{
    public ItemPecaDtoValidator()
    {
        RuleFor(x => x.PecaId).NotEqual(Guid.Empty);
        RuleFor(x => x.Quantidade).GreaterThan(0);
    }
}
