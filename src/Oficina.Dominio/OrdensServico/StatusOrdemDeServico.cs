namespace Oficina.Dominio.OrdensServico;

public enum StatusOrdemDeServico
{
    Recebida = 1,
    EmDiagnostico = 2,
    AguardandoAprovacao = 3,
    EmExecucao = 4,
    Finalizada = 5,
    Entregue = 6,
    Cancelada = 7
}
