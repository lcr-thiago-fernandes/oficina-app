using System.Collections.Concurrent;
using Oficina.Infraestrutura.Mensageria.Saga;

namespace Oficina.Infraestrutura.Testes.Mensageria;

/// <summary>Guarda em memória os passos registrados pela saga (a trilha real é da Task 6).</summary>
public sealed class RegistradorPassoSagaFake : IRegistradorPassoSaga
{
    private readonly ConcurrentQueue<(string Passo, string Resultado, bool Compensacao, string? Detalhe)> _registros = new();

    public IReadOnlyList<(string Passo, string Resultado, bool Compensacao, string? Detalhe)> Registros =>
        _registros.ToList();

    /// <summary>Só (passo, resultado, compensação), para asserts que não olham o detalhe.</summary>
    public IReadOnlyList<(string Passo, string Resultado, bool Compensacao)> Passos =>
        _registros.Select(r => (r.Passo, r.Resultado, r.Compensacao)).ToList();

    public void Registrar(EstadoSagaOrdemServico saga, string passo, string resultado, bool compensacao, string? detalhe = null) =>
        _registros.Enqueue((passo, resultado, compensacao, detalhe));

    public void Limpar() => _registros.Clear();
}
