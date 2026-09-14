using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Oficina.Dominio.OrdensServico;

namespace Oficina.Infraestrutura.Persistencia.Configuracoes;

public class OrdemDeServicoConfiguration : IEntityTypeConfiguration<OrdemDeServico>
{
    public void Configure(EntityTypeBuilder<OrdemDeServico> b)
    {
        b.ToTable("ordem_servico", "os");
        b.HasKey(x => x.Id);

        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.Numero)
            .HasColumnName("numero")
            .ValueGeneratedOnAdd()
            .UseSerialColumn(); // BIGSERIAL via Npgsql
        b.HasIndex(x => x.Numero).IsUnique();

        b.Property(x => x.ClienteId).HasColumnName("cliente_id").IsRequired();
        b.Property(x => x.VeiculoId).HasColumnName("veiculo_id").IsRequired();

        b.Property(x => x.Status)
            .HasConversion<string>().HasColumnName("status").HasMaxLength(30).IsRequired();
        // Padrão de acesso real: filtrar por status e ordenar por data de abertura.
        // `ordem_servico` não possui coluna `atualizado_em`; `criada_em` é a data canônica.
        b.HasIndex(x => new { x.Status, x.CriadaEm })
            .HasDatabaseName("ix_ordem_servico_status_data");
        b.HasIndex(x => x.ClienteId);

        b.Property(x => x.Observacoes).HasColumnName("observacoes").HasColumnType("text");

        b.Property(x => x.Unidade)
            .HasColumnName("unidade")
            .HasMaxLength(60)
            .HasDefaultValue("matriz")
            .IsRequired();
        b.HasIndex(x => x.Unidade).HasDatabaseName("ix_ordem_servico_unidade");

        b.Property(x => x.CriadaEm).HasColumnName("criada_em").IsRequired();
        b.Property(x => x.DiagnosticadaEm).HasColumnName("diagnosticada_em");
        b.Property(x => x.EnviadaAprovacaoEm).HasColumnName("enviada_aprovacao_em");
        b.Property(x => x.OrcamentoAprovadoEm).HasColumnName("orcamento_aprovado_em");
        b.Property(x => x.OrcamentoRejeitadoEm).HasColumnName("orcamento_rejeitado_em");
        b.Property(x => x.IniciadaEm).HasColumnName("iniciada_em");
        b.Property(x => x.FinalizadaEm).HasColumnName("finalizada_em");
        b.Property(x => x.EntregueEm).HasColumnName("entregue_em");

        // Totais e duração — campos calculados, não persistidos
        b.Ignore(x => x.TotalServicos);
        b.Ignore(x => x.TotalPecas);
        b.Ignore(x => x.TotalGeral);
        b.Ignore(x => x.DuracaoExecucao);

        b.HasMany(x => x.ItensServico)
            .WithOne()
            .HasForeignKey("ordem_servico_id")
            .OnDelete(DeleteBehavior.Cascade);

        b.HasMany(x => x.ItensPeca)
            .WithOne()
            .HasForeignKey("ordem_servico_id")
            .OnDelete(DeleteBehavior.Cascade);

        b.HasMany(x => x.Historico)
            .WithOne()
            .HasForeignKey("ordem_servico_id")
            .OnDelete(DeleteBehavior.Cascade);

        b.Metadata.FindNavigation(nameof(OrdemDeServico.ItensServico))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
        b.Metadata.FindNavigation(nameof(OrdemDeServico.ItensPeca))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
        b.Metadata.FindNavigation(nameof(OrdemDeServico.Historico))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}
