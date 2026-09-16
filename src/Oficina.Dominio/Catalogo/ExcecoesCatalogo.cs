namespace Oficina.Dominio.Catalogo;

public class ServicoInvalidoException : ExcecaoDeDominio
{
    public ServicoInvalidoException(string mensagem) : base(mensagem) { }
}
