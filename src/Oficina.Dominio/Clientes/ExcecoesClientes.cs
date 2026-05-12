namespace Oficina.Dominio.Clientes;

public class DocumentoInvalidoException : Exception
{
    public DocumentoInvalidoException(string mensagem) : base(mensagem) { }
}

public class EmailInvalidoException : Exception
{
    public EmailInvalidoException(string mensagem) : base(mensagem) { }
}

public class PlacaInvalidaException : Exception
{
    public PlacaInvalidaException(string mensagem) : base(mensagem) { }
}

public class ClienteInvalidoException : Exception
{
    public ClienteInvalidoException(string mensagem) : base(mensagem) { }
}

public class VeiculoNaoEncontradoException : Exception
{
    public VeiculoNaoEncontradoException(string placa)
        : base($"Veículo com placa '{placa}' não encontrado neste cliente.") { }
}

public class PlacaJaCadastradaException : Exception
{
    public PlacaJaCadastradaException(string placa)
        : base($"Placa '{placa}' já cadastrada para este cliente.") { }
}

public class DocumentoJaCadastradoException : Exception
{
    public DocumentoJaCadastradoException(string documento)
        : base($"Já existe cliente cadastrado com o documento '{documento}'.") { }
}
