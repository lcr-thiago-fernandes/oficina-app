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

    /// <summary>
    /// Verifica a senha contra o hash BCrypt armazenado.
    /// </summary>
    /// <remarks>
    /// Sem chamador nesta API de propósito: ela não emite token desde a Fase 3. É a
    /// regra de verificação que corresponde ao hash gravado em <c>auth.usuario</c>
    /// pelo bootstrap e consumido pela função serverless <c>oficina-auth-api</c>
    /// (repositório <c>oficina-lambda-auth</c>). Ver
    /// <c>Oficina.Aplicacao.Auth.BootstrapAdminUseCase</c>.
    /// </remarks>
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
