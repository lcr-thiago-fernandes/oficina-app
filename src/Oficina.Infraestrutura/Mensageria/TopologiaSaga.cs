using System.Reflection;
using MassTransit;
using Oficina.Infraestrutura.Mensageria.Contratos;
using Oficina.Infraestrutura.Mensageria.Internos;

namespace Oficina.Infraestrutura.Mensageria;

/// <summary>
/// Nome de entidade (tópico SNS) por tipo, com o prefixo do ambiente. No 8.4.1 um tipo com
/// <c>[EntityName]</c> ignora o formatter global: o prefixo só entra via <c>SetEntityName</c> por tipo.
/// </summary>
public static class TopologiaSaga
{
    private static readonly Type[] Contratos =
    {
        typeof(GerarOrcamento), typeof(OrcamentoGerado), typeof(OrcamentoFalhou),
        typeof(OrcamentoAprovado), typeof(OrcamentoRejeitado),
        typeof(PagamentoConfirmado), typeof(PagamentoRecusado), typeof(EstornarPagamento),
        typeof(PagamentoEstornado), typeof(EstornoFalhou), typeof(CancelarOrcamento),
        typeof(IniciarExecucao), typeof(ExecucaoEnfileirada), typeof(ExecucaoRecusada),
        typeof(StatusExecucaoAlterado), typeof(ExecucaoFinalizada),
    };

    private static readonly Type[] Internos = { typeof(OrdemServicoAberta), typeof(CancelamentoSolicitado) };

    private static readonly MethodInfo DefinirNomeGenerico =
        typeof(TopologiaSaga).GetMethod(nameof(DefinirNome), BindingFlags.NonPublic | BindingFlags.Static)!;

    /// <summary>Os 16 contratos da saga e os 2 eventos internos do OS Service.</summary>
    public static IReadOnlyList<Type> TiposConfigurados { get; } = Contratos.Concat(Internos).ToArray();

    public static void Aplicar(IBusFactoryConfigurator cfg, NomesMensageria nomes)
    {
        foreach (var tipo in Contratos)
            DefinirNomeGenerico.MakeGenericMethod(tipo).Invoke(null, new object[] { cfg, nomes.NomeTopico(tipo.Name) });
        foreach (var tipo in Internos)
            DefinirNomeGenerico.MakeGenericMethod(tipo).Invoke(null, new object[] { cfg, nomes.NomeTopicoInterno(tipo.Name) });
    }

    private static void DefinirNome<T>(IBusFactoryConfigurator cfg, string nome) where T : class =>
        cfg.Message<T>(m => m.SetEntityName(nome));
}
