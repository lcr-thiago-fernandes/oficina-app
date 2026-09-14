using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Oficina.Dominio.Auth;
using Oficina.Dominio.Catalogo;
using Oficina.Dominio.Clientes;
using Oficina.Dominio.OrdensServico;

namespace Oficina.Infraestrutura.Persistencia;

public class OficinaDbContext : DbContext
{
    public OficinaDbContext(DbContextOptions<OficinaDbContext> options) : base(options)
    {
        // Ver MarcarHistoricoDescobertoViaGrafoComoAdicionado abaixo para o motivo.
        ChangeTracker.Tracked += MarcarHistoricoDescobertoViaGrafoComoAdicionado;
    }

    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<Servico> Servicos => Set<Servico>();
    public DbSet<Oficina.Dominio.Estoque.Peca> Pecas => Set<Oficina.Dominio.Estoque.Peca>();
    public DbSet<Oficina.Dominio.OrdensServico.OrdemDeServico> OrdensServico =>
        Set<Oficina.Dominio.OrdensServico.OrdemDeServico>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("public");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OficinaDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }

    /// <summary>
    /// O histórico de status (<see cref="HistoricoStatus"/>) é um efeito colateral interno
    /// do domínio: cada transição (IniciarDiagnostico, EnviarOrcamentoParaAprovacao, Rejeitar,
    /// IniciarExecucao, Finalizar, Entregar, Criar) acrescenta uma entrada nova à coleção
    /// <see cref="OrdemDeServico.Historico"/> sem passar por Add() explícito — de propósito,
    /// para não vazar esse invariante do agregado para a camada de Aplicação (ao contrário de
    /// ItemServico/ItemPeca, que têm MarcarItemXComoNovo chamado explicitamente em cada Use
    /// Case que os adiciona).
    ///
    /// Como o Id de HistoricoStatus é um Guid atribuído pelo próprio domínio (não gerado pelo
    /// banco), o change tracker do EF Core, ao descobrir uma entrada nova por meio do grafo
    /// (fixup, a partir de uma OrdemDeServico já rastreada), não consegue diferenciá-la de uma
    /// referência a uma linha que já existe no banco — por padrão ele assume Unchanged, o que
    /// gera um UPDATE para uma linha inexistente (DbUpdateConcurrencyException).
    ///
    /// A correção não pode se basear em "a navegação Historico não foi carregada" (abordagem
    /// anterior, via CollectionEntry.IsLoaded): isso quebraria no dia em que uma leitura
    /// existente ganhasse Include(Historico) (o índice ix_ordem_servico_status_data e o
    /// dashboard de tempo médio por status dependem exatamente de ObterPorIdAsync/
    /// ObterPorNumeroAsync carregarem o histórico, para RegistrarTransicao calcular
    /// DuracaoSegundos a partir da última entrada) e falharia silenciosamente (falso positivo)
    /// se uma consulta futura materializasse HistoricoStatus rastreado no mesmo contexto de uma
    /// OS também rastreada, via fixup, sem que a navegação fosse marcada IsLoaded.
    ///
    /// Em vez disso, distinguimos pela ORIGEM do rastreamento: ChangeTracker.Tracked expõe
    /// FromQuery, que é true quando a entidade chegou como resultado de uma consulta (Include,
    /// Load, ToListAsync — ou seja, já existe no banco) e false quando chegou por fixup de
    /// grafo a partir de uma entidade já rastreada (ou seja, só pode ter sido criada agora, em
    /// memória, pelo domínio). Essa é a mesma distinção que EF Core recomenda oficialmente para
    /// entidades com chave já atribuída descobertas via grafo. Duas variantes cogitadas e
    /// descartadas: (1) checar se a FK sombra `ordem_servico_id` ainda não foi atribuída — não
    /// é confiável, porque o fixup de relacionamento tende a atribuí-la antes de determinarmos
    /// Added/Unchanged, na mesma passada de DetectChanges; (2) comparar contra um snapshot de
    /// `Set&lt;HistoricoStatus&gt;().Local` capturado antes da chamada — exigiria um ponto de
    /// captura anterior à execução da transição de domínio, o que devolveria a lógica para a
    /// camada de Aplicação (o problema que estamos evitando).
    /// </summary>
    private static void MarcarHistoricoDescobertoViaGrafoComoAdicionado(object? sender, EntityTrackedEventArgs e)
    {
        if (e.FromQuery) return;
        if (e.Entry.Entity is not HistoricoStatus) return;
        if (e.Entry.State != EntityState.Added)
            e.Entry.State = EntityState.Added;
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ChangeTracker.DetectChanges();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ChangeTracker.DetectChanges();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }
}
