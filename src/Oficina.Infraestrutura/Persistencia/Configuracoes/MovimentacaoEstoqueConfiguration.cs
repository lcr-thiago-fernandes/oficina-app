using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Oficina.Dominio.Estoque;

namespace Oficina.Infraestrutura.Persistencia.Configuracoes;

public class MovimentacaoEstoqueConfiguration : IEntityTypeConfiguration<MovimentacaoEstoque>
{
    private static readonly IReadOnlyDictionary<TipoMovimentacao, string> CodigoPorTipo =
        new Dictionary<TipoMovimentacao, string>
        {
            [TipoMovimentacao.Entrada] = "E",
            [TipoMovimentacao.Saida] = "S",
            [TipoMovimentacao.Reserva] = "R",
            [TipoMovimentacao.LiberacaoReserva] = "L",
            [TipoMovimentacao.Reposicao] = "P"
        };

    private static readonly IReadOnlyDictionary<string, TipoMovimentacao> TipoPorCodigo =
        CodigoPorTipo.ToDictionary(kv => kv.Value, kv => kv.Key);

    private static string ParaCodigo(TipoMovimentacao tipo) =>
        CodigoPorTipo.TryGetValue(tipo, out var codigo)
            ? codigo
            : throw new InvalidOperationException($"Tipo de movimentação sem código persistido: {tipo}.");

    private static TipoMovimentacao ParaTipo(string codigo) =>
        TipoPorCodigo.TryGetValue(codigo, out var tipo)
            ? tipo
            : throw new InvalidOperationException($"Código de movimentação desconhecido: '{codigo}'.");

    public void Configure(EntityTypeBuilder<MovimentacaoEstoque> b)
    {
        b.ToTable("movimentacao", "estoque");
        b.HasKey(x => x.Id);

        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.Tipo)
            .HasConversion(
                v => ParaCodigo(v),
                v => ParaTipo(v))
            .HasColumnName("tipo").HasColumnType("char(1)").IsRequired();
        b.Property(x => x.Quantidade).HasColumnName("quantidade").IsRequired();
        b.Property(x => x.Motivo).HasColumnName("motivo").HasMaxLength(100).IsRequired();
        b.Property(x => x.OrdemServicoId).HasColumnName("ordem_servico_id");
        b.Property(x => x.CriadoEm).HasColumnName("criado_em").IsRequired();
        b.Property<Guid>("peca_id");

        b.HasIndex("peca_id", nameof(MovimentacaoEstoque.CriadoEm)).HasDatabaseName("ix_movimentacao_peca_data");
        b.ToTable(t => t.HasCheckConstraint("ck_movimentacao_quantidade_positiva", "quantidade > 0"));
    }
}
