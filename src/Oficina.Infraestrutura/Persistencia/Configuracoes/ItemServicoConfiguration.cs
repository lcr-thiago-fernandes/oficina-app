using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Oficina.Dominio.OrdensServico;

namespace Oficina.Infraestrutura.Persistencia.Configuracoes;

public class ItemServicoConfiguration : IEntityTypeConfiguration<ItemServico>
{
    public void Configure(EntityTypeBuilder<ItemServico> b)
    {
        b.ToTable("item_servico", "os");
        b.HasKey(x => x.Id);

        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.ServicoId).HasColumnName("servico_id").IsRequired();
        b.Property(x => x.ServicoNome).HasColumnName("servico_nome").HasMaxLength(120).IsRequired();
        b.Property(x => x.PrecoSnapshot)
            .HasColumnName("preco_snapshot").HasColumnType("decimal(10,2)").IsRequired();
        b.Property(x => x.Quantidade).HasColumnName("quantidade").IsRequired();
        b.Property<Guid>("ordem_servico_id");
        b.Ignore(x => x.Subtotal);
    }
}
