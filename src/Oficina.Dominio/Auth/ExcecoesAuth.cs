namespace Oficina.Dominio.Auth;

public class SenhaInvalidaException : Exception
{
    public SenhaInvalidaException(string mensagem) : base(mensagem) { }
}

public class CredenciaisInvalidasException : Exception
{
    public CredenciaisInvalidasException()
        : base("Usuário ou senha inválidos.") { }
}

public class UsuarioInativoException : Exception
{
    public UsuarioInativoException()
        : base("Usuário inativo.") { }
}
