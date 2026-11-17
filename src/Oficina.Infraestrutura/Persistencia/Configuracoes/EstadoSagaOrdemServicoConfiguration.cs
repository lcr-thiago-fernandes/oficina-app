using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Oficina.Infraestrutura.Mensageria.Saga;

namespace Oficina.Infraestrutura.Persistencia.Configuracoes;

public class EstadoSagaOrdemServicoConfiguration : IEntityTypeConfiguration<EstadoSagaOrdemServico>
{
    public void Configure(EntityTypeBuilder<EstadoSagaOrdemServico> b)
    {
        b.ToTable("ordem_servico_saga", "saga");
        b.HasKey(x => x.CorrelationId);

        b.Property(x => x.CorrelationId).HasColumnName("ordem_servico_id").ValueGeneratedNever();
        // Nulo só dentro da transação do primeiro evento: o repositório insere a instância (InsertOnInitial)
        // antes de a máquina definir o estado inicial.
        b.Property(x => x.EstadoAtual).HasColumnName("estado").HasMaxLength(64).IsRequired(false);
        b.Property(x => x.NumeroOs).HasColumnName("numero_os");
        // 128: ids do Mercado Pago são numéricos e curtos; os do simulador do Billing passam de 64 (sim-a-{osId}-{valor}-{guid}).
        b.Property(x => x.PagamentoId).HasColumnName("pagamento_id").HasMaxLength(128);
        b.Property(x => x.Motivo).HasColumnName("motivo").HasColumnType("text");
        b.Property(x => x.CriadaEm).HasColumnName("criada_em");
        b.Property(x => x.AtualizadaEm).HasColumnName("atualizada_em");

        // Concorrência otimista do repositório da saga (ConcurrencyMode.Optimistic).
        // `xmin` é a coluna de sistema do PostgreSQL — não é criada pela migration.
        b.Property(x => x.Versao).HasColumnName("xmin").HasColumnType("xid").IsRowVersion();
    }
}
