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
            .FirstOrDefaultAsync(o => o.Id == id, ct);

    public Task<OrdemDeServico?> ObterPorNumeroAsync(long numero, CancellationToken ct) =>
        _db.OrdensServico
            .Include(o => o.ItensServico)
            .Include(o => o.ItensPeca)
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
        if (status.HasValue) q = q.Where(o => o.Status == status.Value);

        return await q
            .OrderByDescending(o => o.CriadaEm)
            .Skip((pagina - 1) * tamanhoPagina)
            .Take(tamanhoPagina)
            .ToListAsync(ct);
    }

    public Task<int> ContarAsync(StatusOrdemDeServico? status, CancellationToken ct)
    {
        var q = _db.OrdensServico.AsQueryable();
        if (status.HasValue) q = q.Where(o => o.Status == status.Value);
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
