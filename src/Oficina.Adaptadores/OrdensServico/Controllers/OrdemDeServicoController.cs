using Oficina.Adaptadores.OrdensServico.Presenters;
using Oficina.Aplicacao.OrdensServico;
using Oficina.Aplicacao.OrdensServico.Dtos;
using Oficina.Aplicacao.OrdensServico.Saga.Dtos;

namespace Oficina.Adaptadores.OrdensServico.Controllers;

/// <summary>Ordem apresentada e a forma como o cancelamento foi tratado (a Api mapeia para 200 ou 202).</summary>
public sealed record RespostaCancelamento(OrdemResponse Ordem, FormaCancelamento Forma);

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
    private readonly CancelarOrdemUseCase _cancelar;
    private readonly ObterSagaDaOrdemUseCase _obterSaga;

    public OrdemDeServicoController(
        AbrirOrdemDeServicoUseCase abrir,
        ObterOrdemPorIdUseCase obter,
        ListarOrdensUseCase listar,
        EntregarOrdemUseCase entregar,
        ObterTempoMedioExecucaoUseCase tempoMedio,
        CancelarOrdemUseCase cancelar,
        ObterSagaDaOrdemUseCase obterSaga)
    {
        _abrir = abrir;
        _obter = obter;
        _listar = listar;
        _entregar = entregar;
        _tempoMedio = tempoMedio;
        _cancelar = cancelar;
        _obterSaga = obterSaga;
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

    public async Task<RespostaCancelamento?> CancelarAsync(Guid id, CancelarOrdemRequest req, CancellationToken ct)
    {
        var r = await _cancelar.ExecutarAsync(id, req.Motivo, ct);
        return r is null ? null : new RespostaCancelamento(OrdemDeServicoPresenter.Apresentar(r.Ordem), r.Forma);
    }

    public Task<EstadoSagaResponse?> ObterSagaAsync(Guid id, CancellationToken ct) =>
        _obterSaga.ExecutarAsync(id, ct);

    public async Task<MetricasTempoMedioResponse> ObterTempoMedioExecucaoAsync(CancellationToken ct)
    {
        var metrica = await _tempoMedio.ExecutarAsync(ct);
        return OrdemDeServicoPresenter.ApresentarMetrica(metrica);
    }
}
