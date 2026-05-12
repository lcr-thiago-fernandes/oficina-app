using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Oficina.Dominio.Auth;

namespace Oficina.Infraestrutura.Persistencia.Configuracoes;

public class UsuarioConfiguration : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> b)
    {
        b.ToTable("usuario", "auth");

        b.HasKey(u => u.Id);

        b.Property(u => u.Id).HasColumnName("id");

        b.Property(u => u.Username)
            .HasConversion(
                v => v.Valor,
                v => Username.Criar(v))
            .HasColumnName("username")
            .HasMaxLength(50)
            .IsRequired();

        b.HasIndex(u => u.Username).IsUnique();

        b.Property(u => u.Senha)
            .HasConversion(
                v => v.Hash,
                v => Senha.DeHashExistente(v))
            .HasColumnName("password_hash")
            .HasMaxLength(255)
            .IsRequired();

        b.Property(u => u.Perfil)
            .HasConversion<string>()
            .HasColumnName("perfil")
            .HasMaxLength(20)
            .IsRequired();

        b.Property(u => u.Ativo).HasColumnName("ativo").IsRequired();
        b.Property(u => u.PrecisaTrocarSenha).HasColumnName("precisa_trocar_senha").IsRequired();
        b.Property(u => u.CriadoEm).HasColumnName("criado_em").IsRequired();
    }
}
