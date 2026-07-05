using Oficina.Adaptadores.Catalogo.Presenters;
using Oficina.Aplicacao.Catalogo;
using Oficina.Aplicacao.Catalogo.Dtos;

namespace Oficina.Adaptadores.Catalogo.Controllers;

// Controller de aplicação (Adaptadores de Interface): orquestra os casos de uso e formata via Presenter.
public class ServicoController
{
    private readonly CriarServicoUseCase _criar;
    private readonly ObterServicoPorIdUseCase _obter;
    private readonly ListarServicosUseCase _listar;
    private readonly AtualizarServicoUseCase _atualizar;
    private readonly RemoverServicoUseCase _remover;

    public ServicoController(
        CriarServicoUseCase criar,
        ObterServicoPorIdUseCase obter,
        ListarServicosUseCase listar,
        AtualizarServicoUseCase atualizar,
        RemoverServicoUseCase remover)
    {
        _criar = criar;
        _obter = obter;
        _listar = listar;
        _atualizar = atualizar;
        _remover = remover;
    }

    public async Task<ServicoResponse> CriarAsync(CriarServicoRequest req, CancellationToken ct)
    {
        var servico = await _criar.ExecutarAsync(req, ct);
        return ServicoPresenter.Apresentar(servico);
    }

    public async Task<ServicoResponse?> ObterPorIdAsync(Guid id, CancellationToken ct)
    {
        var servico = await _obter.ExecutarAsync(id, ct);
        return servico is null ? null : ServicoPresenter.Apresentar(servico);
    }

    public async Task<PaginaServicos> ListarAsync(string? nome, int pagina, int tamanhoPagina, bool incluirInativos, CancellationToken ct)
    {
        var r = await _listar.ExecutarAsync(nome, pagina, tamanhoPagina, incluirInativos, ct);
        return ServicoPresenter.ApresentarPagina(r.Itens, r.Total, r.Pagina, r.TamanhoPagina);
    }

    public async Task<ServicoResponse?> AtualizarAsync(Guid id, AtualizarServicoRequest req, CancellationToken ct)
    {
        var servico = await _atualizar.ExecutarAsync(id, req, ct);
        return servico is null ? null : ServicoPresenter.Apresentar(servico);
    }

    public Task<bool> RemoverAsync(Guid id, CancellationToken ct) => _remover.ExecutarAsync(id, ct);
}
