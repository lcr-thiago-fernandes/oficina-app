namespace Oficina.Dominio.OrdensServico;

public class TransicaoDeStatusInvalidaException : Exception
{
    public TransicaoDeStatusInvalidaException(StatusOrdemDeServico atual, string acao)
        : base($"Não é possível {acao} a partir do status '{atual}'.") { }
}

public class OrdemSemItensException : Exception
{
    public OrdemSemItensException()
        : base("A ordem precisa ter ao menos um item antes de enviar para aprovação.") { }
}

public class ItemNaoEncontradoException : Exception
{
    public ItemNaoEncontradoException(string tipo, Guid id)
        : base($"Item de {tipo} com id '{id}' não encontrado nesta ordem.") { }
}

public class OrcamentoNaoAprovadoException : Exception
{
    public OrcamentoNaoAprovadoException()
        : base("Não é possível iniciar execução sem aprovação do orçamento pelo cliente.") { }
}

public class ItemInvalidoException : Exception
{
    public ItemInvalidoException(string mensagem) : base(mensagem) { }
}

public class OrdemImutavelException : Exception
{
    public OrdemImutavelException()
        : base("Itens só podem ser modificados antes do início da execução.") { }
}

public abstract record EventoOs(DateTimeOffset Ocorreu);
public sealed record OrdemDeServicoCriadaEvent(Guid OrdemId, DateTimeOffset Ocorreu) : EventoOs(Ocorreu);
public sealed record ExecucaoIniciadaEvent(Guid OrdemId, IReadOnlyList<(Guid PecaId, int Quantidade)> Pecas, DateTimeOffset Ocorreu) : EventoOs(Ocorreu);
public sealed record OrdemFinalizadaEvent(Guid OrdemId, DateTimeOffset Ocorreu) : EventoOs(Ocorreu);
public sealed record OrdemEntregueEvent(Guid OrdemId, DateTimeOffset Ocorreu) : EventoOs(Ocorreu);
