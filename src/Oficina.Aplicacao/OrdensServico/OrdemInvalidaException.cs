using Oficina.Dominio;

namespace Oficina.Aplicacao.OrdensServico;

/// <summary>
/// Violação de regra de negócio detectada no caso de uso (não no agregado):
/// cliente inativo, serviço/peça inexistente ou inativo. É erro do chamador
/// (422), por isso herda <see cref="ExcecaoDeDominio"/> e não conta como falha
/// de processamento na telemetria.
/// </summary>
public class OrdemInvalidaException : ExcecaoDeDominio
{
    public OrdemInvalidaException(string mensagem) : base(mensagem) { }
}
