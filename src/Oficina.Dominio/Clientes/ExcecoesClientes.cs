namespace Oficina.Dominio.Clientes;

public class DocumentoInvalidoException : ExcecaoDeDominio
{
    public DocumentoInvalidoException(string mensagem) : base(mensagem) { }
}

public class EmailInvalidoException : ExcecaoDeDominio
{
    public EmailInvalidoException(string mensagem) : base(mensagem) { }
}

public class PlacaInvalidaException : ExcecaoDeDominio
{
    public PlacaInvalidaException(string mensagem) : base(mensagem) { }
}

public class ClienteInvalidoException : ExcecaoDeDominio
{
    public ClienteInvalidoException(string mensagem) : base(mensagem) { }
}

public class VeiculoNaoEncontradoException : ExcecaoDeDominio
{
    public VeiculoNaoEncontradoException(string placa)
        : base($"Veículo com placa '{placa}' não encontrado neste cliente.") { }
}

public class PlacaJaCadastradaException : ExcecaoDeDominio
{
    public PlacaJaCadastradaException(string placa)
        : base($"Placa '{placa}' já cadastrada para este cliente.") { }
}

public class DocumentoJaCadastradoException : ExcecaoDeDominio
{
    public DocumentoJaCadastradoException(string documento)
        : base($"Já existe cliente cadastrado com o documento '{documento}'.") { }
}
