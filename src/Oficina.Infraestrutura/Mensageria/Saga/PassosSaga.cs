namespace Oficina.Infraestrutura.Mensageria.Saga;

/// <summary>Nomes de passo e de resultado gravados na trilha da saga.</summary>
public static class PassosSaga
{
    public const string SolicitarOrcamento = nameof(SolicitarOrcamento);
    public const string GerarOrcamento = nameof(GerarOrcamento);
    public const string AprovarOrcamento = nameof(AprovarOrcamento);
    public const string ConfirmarPagamento = nameof(ConfirmarPagamento);
    public const string EnfileirarExecucao = nameof(EnfileirarExecucao);
    public const string Execucao = nameof(Execucao);
    public const string FinalizarExecucao = nameof(FinalizarExecucao);
    public const string Cancelamento = nameof(Cancelamento);
    public const string EstornarPagamento = nameof(EstornarPagamento);

    public const string Sucesso = nameof(Sucesso);
    public const string Falha = nameof(Falha);
    public const string Compensado = nameof(Compensado);
    public const string CompensacaoFalhou = nameof(CompensacaoFalhou);
    public const string Ignorado = nameof(Ignorado);
}
