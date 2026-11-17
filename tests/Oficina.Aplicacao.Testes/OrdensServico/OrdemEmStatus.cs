using Oficina.Dominio.OrdensServico;

namespace Oficina.Aplicacao.Testes.OrdensServico;

/// <summary>
/// Leva uma OS até o status desejado pela cadeia de transições do domínio (Fase 4):
/// Recebida → AguardandoAprovacao → AguardandoPagamento → EmFilaExecucao → EmDiagnostico
/// → EmReparo → Finalizada → Entregue. Cancelada é alcançada rejeitando o orçamento.
/// </summary>
internal static class OrdemEmStatus
{
    public static OrdemDeServico Criar(StatusOrdemDeServico alvo, Guid? clienteId = null)
    {
        var os = OrdemDeServico.Criar(clienteId ?? Guid.NewGuid(), Guid.NewGuid());
        os.AdicionarItemServico(Guid.NewGuid(), "S", 10m, 1);
        Avancar(os, alvo);
        return os;
    }

    public static void Avancar(OrdemDeServico os, StatusOrdemDeServico alvo)
    {
        if (alvo == StatusOrdemDeServico.Cancelada)
        {
            if (os.Status == StatusOrdemDeServico.Recebida) os.AguardarAprovacao();
            os.Rejeitar();
            return;
        }

        while (os.Status != alvo)
        {
            switch (os.Status)
            {
                case StatusOrdemDeServico.Recebida: os.AguardarAprovacao(); break;
                case StatusOrdemDeServico.AguardandoAprovacao: os.AguardarPagamento(); break;
                case StatusOrdemDeServico.AguardandoPagamento: os.EnfileirarExecucao(); break;
                case StatusOrdemDeServico.EmFilaExecucao: os.IniciarDiagnostico(); break;
                case StatusOrdemDeServico.EmDiagnostico: os.IniciarReparo(); break;
                case StatusOrdemDeServico.EmReparo: os.Finalizar(); break;
                case StatusOrdemDeServico.Finalizada: os.Entregar(); break;
                default:
                    throw new InvalidOperationException($"Não há como levar a OS de {os.Status} até {alvo}.");
            }
        }
    }
}
