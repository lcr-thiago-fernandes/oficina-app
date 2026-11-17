using Oficina.Dominio.OrdensServico;

namespace Oficina.Aplicacao.OrdensServico.Dtos;

/// <summary>Como o pedido de cancelamento foi tratado (o controller mapeia para o status HTTP).</summary>
public enum FormaCancelamento
{
    /// <summary>OS com saga: <c>CancelamentoSolicitado</c> enfileirado, a saga cancela depois (202).</summary>
    Solicitado,

    /// <summary>OS da Fase 3, sem saga: cancelada na própria requisição (200).</summary>
    Concluido
}

public sealed record ResultadoCancelamentoOrdem(OrdemDeServico Ordem, FormaCancelamento Forma);
