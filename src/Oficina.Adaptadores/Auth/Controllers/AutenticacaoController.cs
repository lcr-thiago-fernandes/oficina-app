using Oficina.Aplicacao.Auth;

namespace Oficina.Adaptadores.Auth.Controllers;

// Controller de aplicação (Adaptadores de Interface): orquestra o caso de uso de login
// e devolve o union ResultadoLogin. NÃO há Presenter em Auth (desvio consciente vs. os
// demais contextos): o LoginResponse já é montado pelo LoginUseCase na variante Sucesso e
// o mapeamento union→status HTTP (200/401/403) fica no AuthController (camada HTTP).
public class AutenticacaoController
{
    private readonly LoginUseCase _login;

    public AutenticacaoController(LoginUseCase login) => _login = login;

    public Task<ResultadoLogin> LoginAsync(LoginRequest req, CancellationToken ct) =>
        _login.ExecutarAsync(req, ct);
}
