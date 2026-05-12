namespace Oficina.Dominio.Estoque;

public class PecaInvalidaException : Exception
{
    public PecaInvalidaException(string mensagem) : base(mensagem) { }
}

public class SkuInvalidoException : Exception
{
    public SkuInvalidoException(string mensagem) : base(mensagem) { }
}

public class SaldoInsuficienteException : Exception
{
    public SaldoInsuficienteException(string sku, int saldo, int solicitado)
        : base($"Saldo insuficiente para a peça '{sku}': saldo={saldo}, solicitado={solicitado}.")
    { }
}

public class MovimentacaoInvalidaException : Exception
{
    public MovimentacaoInvalidaException(string mensagem) : base(mensagem) { }
}

public class SkuJaCadastradoException : Exception
{
    public SkuJaCadastradoException(string sku)
        : base($"Já existe peça com SKU '{sku}'.") { }
}
