namespace Oficina.Dominio.OrdensServico;

public class TransicaoDeStatusInvalidaException : ExcecaoDeDominio
{
    public TransicaoDeStatusInvalidaException(StatusOrdemDeServico atual, string acao)
        : base($"Não é possível {acao} a partir do status '{atual}'.") { }
}

public class OrdemSemItensException : ExcecaoDeDominio
{
    public OrdemSemItensException()
        : base("A ordem precisa ter ao menos um item antes de enviar para aprovação.") { }
}

public class ItemNaoEncontradoException : ExcecaoDeDominio
{
    public ItemNaoEncontradoException(string tipo, Guid id)
        : base($"Item de {tipo} com id '{id}' não encontrado nesta ordem.") { }
}

public class OrcamentoNaoAprovadoException : ExcecaoDeDominio
{
    public OrcamentoNaoAprovadoException()
        : base("Não é possível iniciar execução sem aprovação do orçamento pelo cliente.") { }
}

public class ItemInvalidoException : ExcecaoDeDominio
{
    public ItemInvalidoException(string mensagem) : base(mensagem) { }
}

public class OrdemImutavelException : ExcecaoDeDominio
{
    public OrdemImutavelException()
        : base("Itens só podem ser modificados antes do início da execução.") { }
}
