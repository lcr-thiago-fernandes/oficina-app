using Microsoft.EntityFrameworkCore;
using Oficina.Dominio.Auth;
using Oficina.Dominio.Catalogo;
using Oficina.Dominio.Clientes;

namespace Oficina.Infraestrutura.Persistencia;

public class OficinaDbContext : DbContext
{
    public OficinaDbContext(DbContextOptions<OficinaDbContext> options) : base(options) { }

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
}
