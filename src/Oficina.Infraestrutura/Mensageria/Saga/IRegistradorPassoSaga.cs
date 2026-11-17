namespace Oficina.Infraestrutura.Mensageria.Saga;

/// <summary>
/// Grava um passo na trilha da saga (e atualiza <see cref="EstadoSagaOrdemServico.AtualizadaEm"/>).
/// Chamado dentro do consumo da mensagem: a gravação vai na mesma transação do outbox.
/// </summary>
public interface IRegistradorPassoSaga
{
    void Registrar(EstadoSagaOrdemServico saga, string passo, string resultado, bool compensacao, string? detalhe = null, string? motivo = null);
}
