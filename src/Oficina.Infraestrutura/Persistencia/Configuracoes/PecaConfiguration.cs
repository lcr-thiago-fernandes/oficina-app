using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Oficina.Dominio.Estoque;

namespace Oficina.Infraestrutura.Persistencia.Configuracoes;

public class PecaConfiguration : IEntityTypeConfiguration<Peca>
{
    public void Configure(EntityTypeBuilder<Peca> b)
    {
        b.ToTable("peca", "estoque");
        b.HasKey(x => x.Id);

        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.Nome).HasColumnName("nome").HasMaxLength(120).IsRequired();
        b.Property(x => x.PrecoUnitario)
            .HasColumnName("preco_unitario").HasColumnType("decimal(10,2)").IsRequired();
        b.Property(x => x.SaldoAtual).HasColumnName("saldo_atual").IsRequired();
        b.Property(x => x.Ativo).HasColumnName("ativo").IsRequired();
        b.Property(x => x.CriadoEm).HasColumnName("criado_em").IsRequired();

        b.OwnsOne(x => x.Sku, sku =>
        {
            sku.Property(s => s.Valor).HasColumnName("sku").HasMaxLength(50).IsRequired();
            sku.HasIndex(s => s.Valor).IsUnique();
        });

        b.HasMany(x => x.Movimentacoes)
            .WithOne()
            .HasForeignKey("peca_id")
            .OnDelete(DeleteBehavior.Cascade);

        b.Metadata.FindNavigation(nameof(Peca.Movimentacoes))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}
