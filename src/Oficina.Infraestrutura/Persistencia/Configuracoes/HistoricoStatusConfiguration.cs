using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Oficina.Dominio.OrdensServico;

namespace Oficina.Infraestrutura.Persistencia.Configuracoes;

public class HistoricoStatusConfiguration : IEntityTypeConfiguration<HistoricoStatus>
{
    public void Configure(EntityTypeBuilder<HistoricoStatus> b)
    {
        b.ToTable("historico_status", "os");
        b.HasKey(x => x.Id);

        b.Property(x => x.Id).HasColumnName("id");

        b.Property(x => x.StatusAnterior)
            .HasConversion<string>()
            .HasColumnName("status_anterior")
            .HasMaxLength(30);

        b.Property(x => x.StatusNovo)
            .HasConversion<string>()
            .HasColumnName("status_novo")
            .HasMaxLength(30)
            .IsRequired();

        b.Property(x => x.OcorridoEm).HasColumnName("ocorrido_em").IsRequired();
        b.Property(x => x.DuracaoSegundos).HasColumnName("duracao_segundos");
        // FORA DO ESCOPO DA FASE 3 — sempre NULL. RegistrarTransicao é chamado sem
        // usuarioId por todos os casos de uso; nenhum deles propaga a identidade do
        // chamador até o agregado. A coluna fica como ponto de extensão declarado, não
        // como campo funcional. Ver README, seção "Limitações conhecidas".
        b.Property(x => x.UsuarioId).HasColumnName("usuario_id");

        // Shadow FK para a OS dona do registro — declarada explicitamente para
        // que o indice composto abaixo possa referencia-la independente da
        // ordem de aplicacao das configuracoes.
        b.Property<Guid>("ordem_servico_id");

        // Consulta "linha do tempo de uma OS".
        b.HasIndex("ordem_servico_id", nameof(HistoricoStatus.OcorridoEm))
            .HasDatabaseName("ix_historico_os");

        // Consulta do dashboard: tempo medio por status de origem, em uma janela.
        b.HasIndex(x => new { x.StatusAnterior, x.OcorridoEm })
            .HasDatabaseName("ix_historico_status_data");
    }
}
