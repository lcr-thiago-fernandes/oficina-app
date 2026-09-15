namespace Oficina.Dominio;

/// <summary>
/// Raiz de todas as exceções de regra de negócio do sistema.
/// <para>
/// Serve para um único propósito operacional: distinguir, em tempo de execução,
/// <b>erro do cliente</b> (entrada ou estado que o próprio chamador provocou —
/// mapeado pelo <c>MiddlewareDeExcecoes</c> para 4xx) de <b>falha de
/// processamento</b> (banco fora do ar, bug, timeout — 5xx). A telemetria de
/// negócio (<c>OrdemServicoEvento</c>) só marca <c>resultado='Falha'</c> para a
/// segunda categoria: o alerta Critical da Fase 3 é
/// <c>OrdemServicoEvento WHERE resultado='Falha' &gt; 0</c> e um 422 de transição
/// inválida não pode disparar alerta de produção.
/// </para>
/// <para>
/// Por isso a marcação é um tipo base e não uma lista de tipos espalhada pelo
/// código: uma exceção de domínio nova nasce classificada corretamente, sem
/// precisar lembrar de atualizar um <c>catch ... when</c> em outro arquivo.
/// </para>
/// </summary>
public abstract class ExcecaoDeDominio : Exception
{
    protected ExcecaoDeDominio(string mensagem) : base(mensagem) { }
}
