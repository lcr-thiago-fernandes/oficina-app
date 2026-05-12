using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Oficina.Dominio.Clientes;

namespace Oficina.Infraestrutura.Persistencia.Configuracoes;

public class ClienteConfiguration : IEntityTypeConfiguration<Cliente>
{
    public void Configure(EntityTypeBuilder<Cliente> b)
    {
        b.ToTable("cliente", "clientes");
        b.HasKey(x => x.Id);

        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.Nome).HasColumnName("nome").HasMaxLength(150).IsRequired();
        b.Property(x => x.Ativo).HasColumnName("ativo").IsRequired();
        b.Property(x => x.CriadoEm).HasColumnName("criado_em").IsRequired();
        b.Property(x => x.AtualizadoEm).HasColumnName("atualizado_em").IsRequired();

        b.OwnsOne(x => x.Documento, doc =>
        {
            doc.Property(d => d.Valor).HasColumnName("documento").HasMaxLength(14).IsRequired();
            doc.Property(d => d.Tipo).HasConversion<string>().HasColumnName("tipo_pessoa")
                .HasMaxLength(2).IsRequired();
            doc.HasIndex(d => d.Valor).IsUnique();
        });

        b.OwnsOne(x => x.Email, email =>
        {
            email.Property(e => e.Valor).HasColumnName("email").HasMaxLength(150).IsRequired();
        });

        b.OwnsOne(x => x.Telefone, tel =>
        {
            tel.Property(t => t.Valor).HasColumnName("telefone").HasMaxLength(20).IsRequired();
        });

        b.HasMany(x => x.Veiculos)
            .WithOne()
            .HasForeignKey("cliente_id")
            .OnDelete(DeleteBehavior.Cascade);

        b.Metadata.FindNavigation(nameof(Cliente.Veiculos))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}
