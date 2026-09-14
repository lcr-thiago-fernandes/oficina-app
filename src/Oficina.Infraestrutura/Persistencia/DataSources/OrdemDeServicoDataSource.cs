using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Oficina.Adaptadores.OrdensServico.DataSources;
using Oficina.Aplicacao.OrdensServico.Gateways;
using Oficina.Dominio.OrdensServico;

namespace Oficina.Infraestrutura.Persistencia.DataSources;

public class OrdemDeServicoDataSource : IOrdemDeServicoDataSource
{
    private readonly OficinaDbContext _db;
    public OrdemDeServicoDataSource(OficinaDbContext db) => _db = db;

    public Task<OrdemDeServico?> ObterPorIdAsync(Guid id, CancellationToken ct) =>
        _db.OrdensServico
            .Include(o => o.ItensServico)
            .Include(o => o.ItensPeca)
            // Histórico precisa vir carregado aqui: RegistrarTransicao (chamado por toda
            // transição de status) calcula DuracaoSegundos a partir da última entrada de
            // Historico.LastOrDefault() — sem o Include, a coleção chega vazia e a duração
            // sai sempre nula, inutilizando a coluna que alimenta o dashboard de tempo médio
            // por status. A ordenação por OcorridoEm garante que "a última entrada" seja de
            // fato a mais recente (o Postgres não garante ordem de retorno sem ORDER BY, e o
            // Id de HistoricoStatus é um Guid, sem relação com a ordem cronológica).
            .Include(o => o.Historico.OrderBy(h => h.OcorridoEm))
            .FirstOrDefaultAsync(o => o.Id == id, ct);

    public Task<OrdemDeServico?> ObterPorNumeroAsync(long numero, CancellationToken ct) =>
        _db.OrdensServico
            .Include(o => o.ItensServico)
            .Include(o => o.ItensPeca)
            .Include(o => o.Historico.OrderBy(h => h.OcorridoEm))
            .FirstOrDefaultAsync(o => o.Numero == numero, ct);

    public async Task<IReadOnlyList<OrdemDeServico>> ListarAsync(
        StatusOrdemDeServico? status, int pagina, int tamanhoPagina, CancellationToken ct)
    {
        if (pagina < 1) pagina = 1;
        if (tamanhoPagina < 1 || tamanhoPagina > 100) tamanhoPagina = 20;

        var q = _db.OrdensServico
            .Include(o => o.ItensServico)
            .Include(o => o.ItensPeca)
            .AsQueryable();

        q = OrdemDeServicoQuery.AplicarFiltro(q, status);
        q = OrdemDeServicoQuery.AplicarOrdenacao(q);

        return await q
            .Skip((pagina - 1) * tamanhoPagina)
            .Take(tamanhoPagina)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<OrdemDeServico>> ListarPorClienteAsync(
        Guid clienteId, CancellationToken ct) =>
        await _db.OrdensServico
            .AsNoTracking()
            .Include(o => o.ItensServico)
            .Include(o => o.ItensPeca)
            .Where(o => o.ClienteId == clienteId)
            .OrderByDescending(o => o.CriadaEm)
            .ToListAsync(ct);

    public Task<int> ContarAsync(StatusOrdemDeServico? status, CancellationToken ct)
    {
        var q = OrdemDeServicoQuery.AplicarFiltro(_db.OrdensServico.AsQueryable(), status);
        return q.CountAsync(ct);
    }

    public async Task AdicionarAsync(OrdemDeServico ordem, CancellationToken ct) =>
        await _db.OrdensServico.AddAsync(ordem, ct);

    public Task SalvarAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);

    public void MarcarItemServicoComoNovo(ItemServico item) =>
        _db.Set<ItemServico>().Add(item);

    public void MarcarItemPecaComoNovo(ItemPeca item) =>
        _db.Set<ItemPeca>().Add(item);

    public async Task EmTransacaoSerializadaAsync(Func<CancellationToken, Task> acao, CancellationToken ct)
    {
        IDbContextTransaction? tx = null;
        try
        {
            tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            await acao(ct);
            await tx.CommitAsync(ct);
        }
        catch
        {
            if (tx is not null) await tx.RollbackAsync(ct);
            throw;
        }
        finally
        {
            if (tx is not null) await tx.DisposeAsync();
        }
    }

    public async Task<MetricaTempoMedio> ObterTempoMedioExecucaoAsync(CancellationToken ct)
    {
        // Considera OSs que tiveram início e fim de execução (Finalizada ou Entregue)
        var concluidas = _db.OrdensServico
            .Where(o => o.IniciadaEm != null && o.FinalizadaEm != null);

        var total = await concluidas.CountAsync(ct);
        if (total == 0)
            return new MetricaTempoMedio(0, null, null, null);

        var datas = await concluidas
            .Select(o => new { o.IniciadaEm, o.FinalizadaEm })
            .ToListAsync(ct);

        var duracoes = datas
            .Select(d => (d.FinalizadaEm!.Value - d.IniciadaEm!.Value).TotalMilliseconds)
            .ToList();

        return new MetricaTempoMedio(
            TotalOrdensConcluidas: total,
            TempoMedio: TimeSpan.FromMilliseconds(duracoes.Average()),
            TempoMinimo: TimeSpan.FromMilliseconds(duracoes.Min()),
            TempoMaximo: TimeSpan.FromMilliseconds(duracoes.Max()));
    }
}
