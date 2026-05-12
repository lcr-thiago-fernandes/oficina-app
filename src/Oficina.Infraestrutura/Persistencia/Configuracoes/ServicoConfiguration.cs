using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Oficina.Dominio.Catalogo;

namespace Oficina.Infraestrutura.Persistencia.Configuracoes;

public class ServicoConfiguration : IEntityTypeConfiguration<Servico>
{
    public void Configure(EntityTypeBuilder<Servico> b)
    {
        b.ToTable("servico", "catalogo");
        b.HasKey(x => x.Id);

        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.Nome).HasColumnName("nome").HasMaxLength(120).IsRequired();
        b.Property(x => x.Descricao).HasColumnName("descricao").HasColumnType("text");
        b.Property(x => x.PrecoBase).HasColumnName("preco_base").HasColumnType("decimal(10,2)").IsRequired();
        b.Property(x => x.TempoEstimadoMinutos).HasColumnName("tempo_estimado_minutos").IsRequired();
        b.Property(x => x.Ativo).HasColumnName("ativo").IsRequired();
        b.Property(x => x.CriadoEm).HasColumnName("criado_em").IsRequired();

        b.HasIndex(x => x.Nome);
    }
}
