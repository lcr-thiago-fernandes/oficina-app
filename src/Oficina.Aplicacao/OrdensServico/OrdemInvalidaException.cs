namespace Oficina.Aplicacao.OrdensServico;

public class OrdemInvalidaException : Exception
{
    public OrdemInvalidaException(string mensagem) : base(mensagem) { }
}
