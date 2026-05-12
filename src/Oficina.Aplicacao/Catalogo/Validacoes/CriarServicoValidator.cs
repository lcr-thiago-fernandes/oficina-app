using FluentValidation;
using Oficina.Aplicacao.Catalogo.Dtos;

namespace Oficina.Aplicacao.Catalogo.Validacoes;

public class CriarServicoValidator : AbstractValidator<CriarServicoRequest>
{
    public CriarServicoValidator()
    {
        // PrecoBase > 0 e TempoEstimado > 0 sao validados no dominio
        // (Servico.Criar) — la a excecao vira 422 via MiddlewareDeExcecoes.
        // Mantemos so MaxLength aqui (regra puramente de formato).
        RuleFor(x => x.Nome).MaximumLength(120);
        RuleFor(x => x.Descricao).MaximumLength(2000);
    }
}
