using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Oficina.Dominio.OrdensServico;

namespace Oficina.Infraestrutura.Persistencia.Configuracoes;

public class ItemPecaConfiguration : IEntityTypeConfiguration<ItemPeca>
{
    public void Configure(EntityTypeBuilder<ItemPeca> b)
    {
        b.ToTable("item_peca", "os");
        b.HasKey(x => x.Id);

        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.PecaId).HasColumnName("peca_id").IsRequired();
        b.Property(x => x.PecaNome).HasColumnName("peca_nome").HasMaxLength(120).IsRequired();
        b.Property(x => x.PrecoSnapshot)
            .HasColumnName("preco_snapshot").HasColumnType("decimal(10,2)").IsRequired();
        b.Property(x => x.Quantidade).HasColumnName("quantidade").IsRequired();
        b.Property<Guid>("ordem_servico_id");
        b.Ignore(x => x.Subtotal);

        b.ToTable(t =>
        {
            t.HasCheckConstraint("ck_item_peca_quantidade_positiva", "quantidade > 0");
            t.HasCheckConstraint("ck_item_peca_preco_nao_negativo", "preco_snapshot >= 0");
        });
    }
}
