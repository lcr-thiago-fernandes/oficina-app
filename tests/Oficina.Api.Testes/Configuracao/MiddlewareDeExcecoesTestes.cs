using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Oficina.Api.Configuracao;
using Oficina.Dominio.Estoque;
using Xunit;

namespace Oficina.Api.Testes.Configuracao;

public class MiddlewareDeExcecoesTestes
{
    private static async Task<(int Status, JsonElement Corpo)> Executar(Exception ex)
    {
        var contexto = new DefaultHttpContext();
        contexto.Response.Body = new MemoryStream();
        var middleware = new MiddlewareDeExcecoes(
            _ => throw ex, NullLogger<MiddlewareDeExcecoes>.Instance);

        await middleware.InvokeAsync(contexto);

        contexto.Response.Body.Position = 0;
        var corpo = await JsonSerializer.DeserializeAsync<JsonElement>(contexto.Response.Body);
        return (contexto.Response.StatusCode, corpo);
    }

    private static PostgresException PostgresComSqlState(string sqlState) =>
        new("erro simulado", "ERROR", "ERROR", sqlState);

    private static void DeveSerConflitoDeConcorrencia(int status, JsonElement corpo)
    {
        status.Should().Be(409);
        corpo.GetProperty("title").GetString().Should().Be("Conflito de concorrência");
        corpo.GetProperty("detail").GetString().Should().Be("O recurso foi alterado por outra operação. Tente novamente.");
        corpo.GetProperty("status").GetInt32().Should().Be(409);
    }

    [Fact]
    public async Task ConflitoDeConcorrencia_Retorna409()
    {
        var (status, corpo) = await Executar(new DbUpdateConcurrencyException("xmin mudou"));
        DeveSerConflitoDeConcorrencia(status, corpo);
    }

    [Fact]
    public async Task FalhaDeSerializacaoPostgres_Retorna409()
    {
        var ex = new DbUpdateException("falhou", PostgresComSqlState("40001"));
        var (status, corpo) = await Executar(ex);
        DeveSerConflitoDeConcorrencia(status, corpo);
    }

    [Fact]
    public async Task FalhaDeSerializacaoPostgres_AninhadaEmVariosNiveis_Retorna409()
    {
        var ex = new InvalidOperationException("transação",
            new DbUpdateException("falhou", PostgresComSqlState("40001")));
        var (status, corpo) = await Executar(ex);
        DeveSerConflitoDeConcorrencia(status, corpo);
    }

    [Fact]
    public async Task ViolacaoDeUnicidadePostgres_Retorna409()
    {
        var ex = new DbUpdateException("falhou", PostgresComSqlState("23505"));
        var (status, corpo) = await Executar(ex);
        DeveSerConflitoDeConcorrencia(status, corpo);
    }

    [Fact]
    public async Task OutroErroPostgres_Retorna500()
    {
        var ex = new DbUpdateException("falhou", PostgresComSqlState("23514"));
        var (status, _) = await Executar(ex);
        status.Should().Be(500);
    }

    [Fact]
    public async Task ReservaInvalida_Retorna422()
    {
        var (status, corpo) = await Executar(new ReservaInvalidaException("já confirmada"));

        status.Should().Be(422);
        corpo.GetProperty("title").GetString().Should().Be("Operação inválida");
        corpo.GetProperty("detail").GetString().Should().Be("já confirmada");
    }
}
