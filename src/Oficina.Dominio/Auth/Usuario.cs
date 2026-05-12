namespace Oficina.Dominio.Auth;

public sealed class Usuario
{
    public Guid Id { get; private set; }
    public Username Username { get; private set; } = null!;
    public Senha Senha { get; private set; } = null!;
    public Perfil Perfil { get; private set; }
    public bool Ativo { get; private set; }
    public bool PrecisaTrocarSenha { get; private set; }
    public DateTimeOffset CriadoEm { get; private set; }

    // construtor para o EF Core
    private Usuario() { }

    public static Usuario Criar(Username username, Senha senha, Perfil perfil) => new()
    {
        Id = Guid.NewGuid(),
        Username = username,
        Senha = senha,
        Perfil = perfil,
        Ativo = true,
        PrecisaTrocarSenha = false,
        CriadoEm = DateTimeOffset.UtcNow
    };

    public static Usuario CriarParaBootstrap(Username username, Senha senha)
    {
        var u = Criar(username, senha, Perfil.Admin);
        u.PrecisaTrocarSenha = true;
        return u;
    }

    public void Inativar() => Ativo = false;
    public void Ativar() => Ativo = true;

    public bool Autenticar(string textoPuro)
    {
        if (!Ativo) throw new UsuarioInativoException();
        return Senha.Verificar(textoPuro);
    }

    public void TrocarSenha(Senha nova)
    {
        Senha = nova ?? throw new ArgumentNullException(nameof(nova));
        PrecisaTrocarSenha = false;
    }
}
