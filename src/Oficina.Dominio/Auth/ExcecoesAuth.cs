namespace Oficina.Dominio.Auth;

public class SenhaInvalidaException : ExcecaoDeDominio
{
    public SenhaInvalidaException(string mensagem) : base(mensagem) { }
}

public class CredenciaisInvalidasException : ExcecaoDeDominio
{
    public CredenciaisInvalidasException()
        : base("Usuário ou senha inválidos.") { }
}

public class UsuarioInativoException : ExcecaoDeDominio
{
    public UsuarioInativoException()
        : base("Usuário inativo.") { }
}
