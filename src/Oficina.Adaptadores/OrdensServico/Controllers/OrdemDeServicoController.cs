using Oficina.Adaptadores.OrdensServico.Presenters;
using Oficina.Aplicacao.OrdensServico;
using Oficina.Aplicacao.OrdensServico.Dtos;

namespace Oficina.Adaptadores.OrdensServico.Controllers;

// Controller de aplicação (Adaptadores de Interface): orquestra os casos de uso e formata via Presenter.
public class OrdemDeServicoController
{
    private readonly CriarOrdemUseCase _criar;
    private readonly AbrirOrdemDeServicoUseCase _abrir;
    private readonly ObterOrdemPorIdUseCase _obter;
    private readonly ListarOrdensUseCase _listar;
    private readonly IniciarDiagnosticoUseCase _iniciarDiagnostico;
    private readonly EnviarOrcamentoParaAprovacaoUseCase _enviarOrcamento;
    private readonly IniciarExecucaoUseCase _iniciarExecucao;
    private readonly FinalizarOrdemUseCase _finalizar;
    private readonly EntregarOrdemUseCase _entregar;
    private readonly AdicionarItemServicoUseCase _adicionarItemServico;
    private readonly RemoverItemServicoUseCase _removerItemServico;
    private readonly AdicionarItemPecaUseCase _adicionarItemPeca;
    private readonly RemoverItemPecaUseCase _removerItemPeca;
    private readonly ObterTempoMedioExecucaoUseCase _tempoMedio;
    private readonly RegistrarDecisaoDeOrcamentoUseCase _registrarDecisao;

    public OrdemDeServicoController(
        CriarOrdemUseCase criar,
        AbrirOrdemDeServicoUseCase abrir,
        ObterOrdemPorIdUseCase obter,
        ListarOrdensUseCase listar,
        IniciarDiagnosticoUseCase iniciarDiagnostico,
        EnviarOrcamentoParaAprovacaoUseCase enviarOrcamento,
        IniciarExecucaoUseCase iniciarExecucao,
        FinalizarOrdemUseCase finalizar,
        EntregarOrdemUseCase entregar,
        AdicionarItemServicoUseCase adicionarItemServico,
        RemoverItemServicoUseCase removerItemServico,
        AdicionarItemPecaUseCase adicionarItemPeca,
        RemoverItemPecaUseCase removerItemPeca,
        ObterTempoMedioExecucaoUseCase tempoMedio,
        RegistrarDecisaoDeOrcamentoUseCase registrarDecisao)
    {
        _criar = criar;
        _abrir = abrir;
        _obter = obter;
        _listar = listar;
        _iniciarDiagnostico = iniciarDiagnostico;
        _enviarOrcamento = enviarOrcamento;
        _iniciarExecucao = iniciarExecucao;
        _finalizar = finalizar;
        _entregar = entregar;
        _adicionarItemServico = adicionarItemServico;
        _removerItemServico = removerItemServico;
        _adicionarItemPeca = adicionarItemPeca;
        _removerItemPeca = removerItemPeca;
        _tempoMedio = tempoMedio;
        _registrarDecisao = registrarDecisao;
    }

    public async Task<OrdemResponse> CriarAsync(CriarOrdemRequest req, CancellationToken ct)
    {
        var ordem = await _criar.ExecutarAsync(req, ct);
        return OrdemDeServicoPresenter.Apresentar(ordem);
    }

    public async Task<OrdemResponse> AbrirAsync(AbrirOrdemRequest req, CancellationToken ct)
    {
        var ordem = await _abrir.ExecutarAsync(req, ct);
        return OrdemDeServicoPresenter.Apresentar(ordem);
    }

    public async Task<OrdemResponse?> ObterPorIdAsync(Guid id, CancellationToken ct)
    {
        var ordem = await _obter.ExecutarAsync(id, ct);
        return ordem is null ? null : OrdemDeServicoPresenter.Apresentar(ordem);
    }

    public async Task<PaginaOrdens> ListarAsync(string? status, int pagina, int tamanhoPagina, CancellationToken ct)
    {
        var r = await _listar.ExecutarAsync(status, pagina, tamanhoPagina, ct);
        return OrdemDeServicoPresenter.ApresentarPagina(r.Itens, r.Total, r.Pagina, r.TamanhoPagina);
    }

    public async Task<OrdemResponse?> IniciarDiagnosticoAsync(Guid id, CancellationToken ct)
    {
        var ordem = await _iniciarDiagnostico.ExecutarAsync(id, ct);
        return ordem is null ? null : OrdemDeServicoPresenter.Apresentar(ordem);
    }

    public async Task<OrdemResponse?> EnviarOrcamentoParaAprovacaoAsync(Guid id, CancellationToken ct)
    {
        var ordem = await _enviarOrcamento.ExecutarAsync(id, ct);
        return ordem is null ? null : OrdemDeServicoPresenter.Apresentar(ordem);
    }

    public async Task<OrdemResponse?> IniciarExecucaoAsync(Guid id, CancellationToken ct)
    {
        var ordem = await _iniciarExecucao.ExecutarAsync(id, ct);
        return ordem is null ? null : OrdemDeServicoPresenter.Apresentar(ordem);
    }

    public async Task<OrdemResponse?> FinalizarAsync(Guid id, CancellationToken ct)
    {
        var ordem = await _finalizar.ExecutarAsync(id, ct);
        return ordem is null ? null : OrdemDeServicoPresenter.Apresentar(ordem);
    }

    public async Task<OrdemResponse?> EntregarAsync(Guid id, CancellationToken ct)
    {
        var ordem = await _entregar.ExecutarAsync(id, ct);
        return ordem is null ? null : OrdemDeServicoPresenter.Apresentar(ordem);
    }

    public async Task<ItemServicoResponse?> AdicionarItemServicoAsync(Guid ordemId, AdicionarItemServicoRequest req, CancellationToken ct)
    {
        var item = await _adicionarItemServico.ExecutarAsync(ordemId, req, ct);
        return item is null ? null : OrdemDeServicoPresenter.ApresentarItemServico(item);
    }

    public Task<bool> RemoverItemServicoAsync(Guid ordemId, Guid itemId, CancellationToken ct) =>
        _removerItemServico.ExecutarAsync(ordemId, itemId, ct);

    public async Task<ItemPecaResponse?> AdicionarItemPecaAsync(Guid ordemId, AdicionarItemPecaRequest req, CancellationToken ct)
    {
        var item = await _adicionarItemPeca.ExecutarAsync(ordemId, req, ct);
        return item is null ? null : OrdemDeServicoPresenter.ApresentarItemPeca(item);
    }

    public Task<bool> RemoverItemPecaAsync(Guid ordemId, Guid itemId, CancellationToken ct) =>
        _removerItemPeca.ExecutarAsync(ordemId, itemId, ct);

    public async Task<MetricasTempoMedioResponse> ObterTempoMedioExecucaoAsync(CancellationToken ct)
    {
        var metrica = await _tempoMedio.ExecutarAsync(ct);
        return OrdemDeServicoPresenter.ApresentarMetrica(metrica);
    }

    public async Task<OrdemResponse?> RegistrarDecisaoDeOrcamentoAsync(Guid id, bool aprovado, CancellationToken ct)
    {
        var ordem = await _registrarDecisao.ExecutarAsync(id, aprovado, ct);
        return ordem is null ? null : OrdemDeServicoPresenter.Apresentar(ordem);
    }
}
