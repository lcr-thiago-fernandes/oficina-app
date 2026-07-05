using Oficina.Adaptadores.Estoque.Presenters;
using Oficina.Aplicacao.Estoque;
using Oficina.Aplicacao.Estoque.Dtos;

namespace Oficina.Adaptadores.Estoque.Controllers;

// Controller de aplicação (Adaptadores de Interface): orquestra os casos de uso e formata via Presenter.
public class PecaController
{
    private readonly CriarPecaUseCase _criar;
    private readonly ObterPecaPorIdUseCase _obter;
    private readonly ListarPecasUseCase _listar;
    private readonly AtualizarPecaUseCase _atualizar;
    private readonly RemoverPecaUseCase _remover;
    private readonly RegistrarMovimentacaoUseCase _registrarMovimentacao;
    private readonly ListarMovimentacoesUseCase _listarMovimentacoes;

    public PecaController(
        CriarPecaUseCase criar,
        ObterPecaPorIdUseCase obter,
        ListarPecasUseCase listar,
        AtualizarPecaUseCase atualizar,
        RemoverPecaUseCase remover,
        RegistrarMovimentacaoUseCase registrarMovimentacao,
        ListarMovimentacoesUseCase listarMovimentacoes)
    {
        _criar = criar;
        _obter = obter;
        _listar = listar;
        _atualizar = atualizar;
        _remover = remover;
        _registrarMovimentacao = registrarMovimentacao;
        _listarMovimentacoes = listarMovimentacoes;
    }

    public async Task<PecaResponse> CriarAsync(CriarPecaRequest req, CancellationToken ct)
    {
        var peca = await _criar.ExecutarAsync(req, ct);
        return PecaPresenter.Apresentar(peca);
    }

    public async Task<PecaResponse?> ObterPorIdAsync(Guid id, CancellationToken ct)
    {
        var peca = await _obter.ExecutarAsync(id, ct);
        return peca is null ? null : PecaPresenter.Apresentar(peca);
    }

    public async Task<PaginaPecas> ListarAsync(string? nome, int pagina, int tamanhoPagina, bool incluirInativos, CancellationToken ct)
    {
        var r = await _listar.ExecutarAsync(nome, pagina, tamanhoPagina, incluirInativos, ct);
        return PecaPresenter.ApresentarPagina(r.Itens, r.Total, r.Pagina, r.TamanhoPagina);
    }

    public async Task<PecaResponse?> AtualizarAsync(Guid id, AtualizarPecaRequest req, CancellationToken ct)
    {
        var peca = await _atualizar.ExecutarAsync(id, req, ct);
        return peca is null ? null : PecaPresenter.Apresentar(peca);
    }

    public Task<bool> RemoverAsync(Guid id, CancellationToken ct) => _remover.ExecutarAsync(id, ct);

    public async Task<MovimentacaoResponse?> RegistrarMovimentacaoAsync(Guid pecaId, RegistrarMovimentacaoRequest req, CancellationToken ct)
    {
        var mov = await _registrarMovimentacao.ExecutarAsync(pecaId, req, ct);
        return mov is null ? null : PecaPresenter.ApresentarMovimentacao(mov);
    }

    public async Task<IReadOnlyList<MovimentacaoResponse>?> ListarMovimentacoesAsync(Guid pecaId, CancellationToken ct)
    {
        var movs = await _listarMovimentacoes.ExecutarAsync(pecaId, ct);
        return movs?.Select(PecaPresenter.ApresentarMovimentacao).ToList();
    }
}
