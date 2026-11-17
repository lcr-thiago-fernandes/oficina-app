using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Oficina.Dominio.Auth;
using Oficina.Dominio.Catalogo;
using Oficina.Dominio.Clientes;
using Oficina.Dominio.Estoque;
using Oficina.Dominio.OrdensServico;

namespace Oficina.Api.Configuracao;

public class MiddlewareDeExcecoes
{
    private readonly RequestDelegate _next;
    private readonly ILogger<MiddlewareDeExcecoes> _log;

    public MiddlewareDeExcecoes(RequestDelegate next, ILogger<MiddlewareDeExcecoes> log)
    {
        _next = next;
        _log = log;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            var (status, problema) = MapearExcecao(ex);
            if (status >= 500)
                _log.LogError(ex, "Erro nao mapeado: {Mensagem}", ex.Message);
            else
                _log.LogWarning(ex, "Falha de dominio: {Codigo}", problema.Title);

            context.Response.StatusCode = status;
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsJsonAsync(problema, context.RequestAborted);
        }
    }

    private static (int, ProblemDetails) MapearExcecao(Exception ex) => ex switch
    {
        DocumentoInvalidoException or
        EmailInvalidoException or
        PlacaInvalidaException or
        ClienteInvalidoException or
        SenhaInvalidaException or
        ServicoInvalidoException or
        ArgumentException
            => (StatusCodes.Status422UnprocessableEntity,
                new ProblemDetails { Title = "Dados inválidos", Detail = ex.Message, Status = 422 }),

        PecaInvalidaException or
        SkuInvalidoException or
        MovimentacaoInvalidaException or
        SaldoInsuficienteException or
        ReservaInvalidaException
            => (StatusCodes.Status422UnprocessableEntity,
                new ProblemDetails { Title = "Operação inválida", Detail = ex.Message, Status = 422 }),

        TransicaoDeStatusInvalidaException or
        OrdemSemItensException or
        ItemInvalidoException or
        OrdemImutavelException or
        Oficina.Aplicacao.OrdensServico.OrdemInvalidaException
            => (StatusCodes.Status422UnprocessableEntity,
                new ProblemDetails { Title = "Operação inválida", Detail = ex.Message, Status = 422 }),

        DocumentoJaCadastradoException or
        PlacaJaCadastradaException
            => (StatusCodes.Status409Conflict,
                new ProblemDetails { Title = "Conflito", Detail = ex.Message, Status = 409 }),

        SkuJaCadastradoException
            => (StatusCodes.Status409Conflict,
                new ProblemDetails { Title = "Conflito", Detail = ex.Message, Status = 409 }),

        VeiculoNaoEncontradoException
            => (StatusCodes.Status404NotFound,
                new ProblemDetails { Title = "Não encontrado", Detail = ex.Message, Status = 404 }),

        _ when EhConflitoDeConcorrencia(ex)
            => (StatusCodes.Status409Conflict,
                new ProblemDetails
                {
                    Title = "Conflito de concorrência",
                    Detail = "O recurso foi alterado por outra operação. Tente novamente.",
                    Status = 409
                }),

        _ => (StatusCodes.Status500InternalServerError,
              new ProblemDetails
              {
                  Title = "Erro interno",
                  Status = 500,
                  Detail = $"{ex.GetType().FullName}: {ex.Message}"
                          + (ex.InnerException is null
                              ? string.Empty
                              : $" | INNER: {ex.InnerException.GetType().FullName}: {ex.InnerException.Message}")
              })
    };

    // Concorrência otimista (xmin), falha de serialização (40001, transações Serializable) ou
    // violação de unicidade (23505, p.ex. dois POST concorrentes criando o mesmo cliente/veículo):
    // a operação perdeu a corrida e pode ser repetida pelo cliente.
    private const string FalhaDeSerializacao = "40001";
    private const string ViolacaoDeUnicidade = "23505";

    private static bool EhConflitoDeConcorrencia(Exception ex)
    {
        if (ex is DbUpdateConcurrencyException) return true;

        for (var atual = ex; atual is not null; atual = atual.InnerException)
        {
            if (atual is PostgresException { SqlState: FalhaDeSerializacao or ViolacaoDeUnicidade })
                return true;
        }
        return false;
    }
}
