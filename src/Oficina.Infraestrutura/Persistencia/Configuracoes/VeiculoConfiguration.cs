using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Oficina.Dominio.Clientes;

namespace Oficina.Infraestrutura.Persistencia.Configuracoes;

public class VeiculoConfiguration : IEntityTypeConfiguration<Veiculo>
{
    public void Configure(EntityTypeBuilder<Veiculo> b)
    {
        b.ToTable("veiculo", "clientes");
        b.HasKey(x => x.Id);

        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.Marca).HasColumnName("marca").HasMaxLength(50).IsRequired();
        b.Property(x => x.Modelo).HasColumnName("modelo").HasMaxLength(80).IsRequired();
        b.Property(x => x.Ano).HasColumnName("ano").IsRequired();
        b.Property(x => x.CriadoEm).HasColumnName("criado_em").IsRequired();
        b.Property<Guid>("cliente_id"); // shadow FK

        b.OwnsOne(x => x.Placa, placa =>
        {
            placa.Property(p => p.Valor).HasColumnName("placa").HasMaxLength(7).IsRequired();
            placa.HasIndex(p => p.Valor).IsUnique();
        });
    }
}
