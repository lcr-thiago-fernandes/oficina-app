using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Oficina.Infraestrutura.Mensageria.Saga;

namespace Oficina.Infraestrutura.Persistencia.Configuracoes;

public class PassoSagaConfiguration : IEntityTypeConfiguration<PassoSaga>
{
    public void Configure(EntityTypeBuilder<PassoSaga> b)
    {
        b.ToTable("ordem_servico_saga_passo", "saga");
        b.HasKey(x => x.Id);

        b.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        b.Property(x => x.OrdemServicoId).HasColumnName("ordem_servico_id");
        b.Property(x => x.Passo).HasColumnName("passo").HasMaxLength(64).IsRequired();
        b.Property(x => x.Resultado).HasColumnName("resultado").HasMaxLength(32).IsRequired();
        b.Property(x => x.Compensacao).HasColumnName("compensacao");
        b.Property(x => x.Detalhe).HasColumnName("detalhe").HasColumnType("text");
        b.Property(x => x.Estado).HasColumnName("estado").HasMaxLength(64).IsRequired();
        b.Property(x => x.OcorridoEm).HasColumnName("ocorrido_em");

        b.HasIndex(x => new { x.OrdemServicoId, x.OcorridoEm }).HasDatabaseName("ix_saga_passo_os");
    }
}
