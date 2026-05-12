namespace Oficina.Dominio.Catalogo;

public class ServicoInvalidoException : Exception
{
    public ServicoInvalidoException(string mensagem) : base(mensagem) { }
}
