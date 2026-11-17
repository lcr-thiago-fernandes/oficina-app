namespace Oficina.Dominio.Estoque;

/// <summary>
/// Situação da reserva de uma OS numa peça, derivada do último movimento dessa OS
/// (Reserva → Reservada, Saida → Confirmada, LiberacaoReserva → Liberada, Reposicao → Reposta).
/// </summary>
public enum SituacaoReservaPeca
{
    Nenhuma,
    Reservada,
    Confirmada,
    Liberada,
    Reposta
}
