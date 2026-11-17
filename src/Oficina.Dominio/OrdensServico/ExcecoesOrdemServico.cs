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

public class ItemInvalidoException : ExcecaoDeDominio
{
    public ItemInvalidoException(string mensagem) : base(mensagem) { }
}

public class OrdemImutavelException : ExcecaoDeDominio
{
    public OrdemImutavelException()
        : base("Itens só podem ser incluídos enquanto a ordem está Recebida.") { }
}
