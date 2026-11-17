using System.Collections.Concurrent;
using Oficina.Infraestrutura.Mensageria.Saga;

namespace Oficina.Infraestrutura.Testes.Mensageria;

/// <summary>Guarda em memória os passos registrados pela saga (sem banco).</summary>
public sealed class RegistradorPassoSagaFake : IRegistradorPassoSaga
{
    private readonly ConcurrentQueue<(string Passo, string Resultado, bool Compensacao, string? Detalhe, string Estado)> _registros = new();

    public IReadOnlyList<(string Passo, string Resultado, bool Compensacao, string? Detalhe)> Registros =>
        _registros.Select(r => (r.Passo, r.Resultado, r.Compensacao, r.Detalhe)).ToList();

    /// <summary>Só (passo, resultado, compensação), para asserts que não olham o detalhe.</summary>
    public IReadOnlyList<(string Passo, string Resultado, bool Compensacao)> Passos =>
        _registros.Select(r => (r.Passo, r.Resultado, r.Compensacao)).ToList();

    /// <summary>Estado da saga no momento em que cada passo foi registrado.</summary>
    public IReadOnlyList<string> Estados => _registros.Select(r => r.Estado).ToList();

    public void Registrar(EstadoSagaOrdemServico saga, string passo, string resultado, bool compensacao, string? detalhe = null) =>
        _registros.Enqueue((passo, resultado, compensacao, detalhe, saga.EstadoAtual));

    public void Limpar() => _registros.Clear();
}
