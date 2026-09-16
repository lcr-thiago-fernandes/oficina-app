namespace Oficina.Dominio.Estoque;

public class PecaInvalidaException : ExcecaoDeDominio
{
    public PecaInvalidaException(string mensagem) : base(mensagem) { }
}

public class SkuInvalidoException : ExcecaoDeDominio
{
    public SkuInvalidoException(string mensagem) : base(mensagem) { }
}

public class SaldoInsuficienteException : ExcecaoDeDominio
{
    public SaldoInsuficienteException(string sku, int saldo, int solicitado)
        : base($"Saldo insuficiente para a peça '{sku}': saldo={saldo}, solicitado={solicitado}.")
    { }
}

public class MovimentacaoInvalidaException : ExcecaoDeDominio
{
    public MovimentacaoInvalidaException(string mensagem) : base(mensagem) { }
}

public class SkuJaCadastradoException : ExcecaoDeDominio
{
    public SkuJaCadastradoException(string sku)
        : base($"Já existe peça com SKU '{sku}'.") { }
}
