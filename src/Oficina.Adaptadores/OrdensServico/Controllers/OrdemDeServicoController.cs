using Oficina.Adaptadores.OrdensServico.Presenters;
using Oficina.Aplicacao.OrdensServico;
using Oficina.Aplicacao.OrdensServico.Dtos;

namespace Oficina.Adaptadores.OrdensServico.Controllers;

// Controller de aplicação (Adaptadores de Interface): orquestra os casos de uso e formata via Presenter.
// Na Fase 4 as transições de orçamento, pagamento e execução chegam pela saga (Billing/Execução);
// por HTTP restam abertura, consultas, entrega e métricas.
public class OrdemDeServicoController
{
    private readonly AbrirOrdemDeServicoUseCase _abrir;
    private readonly ObterOrdemPorIdUseCase _obter;
    private readonly ListarOrdensUseCase _listar;
    private readonly EntregarOrdemUseCase _entregar;
    private readonly ObterTempoMedioExecucaoUseCase _tempoMedio;

    public OrdemDeServicoController(
        AbrirOrdemDeServicoUseCase abrir,
        ObterOrdemPorIdUseCase obter,
        ListarOrdensUseCase listar,
        EntregarOrdemUseCase entregar,
        ObterTempoMedioExecucaoUseCase tempoMedio)
    {
        _abrir = abrir;
        _obter = obter;
        _listar = listar;
        _entregar = entregar;
        _tempoMedio = tempoMedio;
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

    public async Task<OrdemResponse?> EntregarAsync(Guid id, CancellationToken ct)
    {
        var ordem = await _entregar.ExecutarAsync(id, ct);
        return ordem is null ? null : OrdemDeServicoPresenter.Apresentar(ordem);
    }

    public async Task<MetricasTempoMedioResponse> ObterTempoMedioExecucaoAsync(CancellationToken ct)
    {
        var metrica = await _tempoMedio.ExecutarAsync(ct);
        return OrdemDeServicoPresenter.ApresentarMetrica(metrica);
    }
}
