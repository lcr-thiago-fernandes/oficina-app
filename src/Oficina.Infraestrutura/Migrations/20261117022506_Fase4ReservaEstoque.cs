using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Oficina.Infraestrutura.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// Fase 4: reserva de estoque (lock semântico da saga). <c>saldo_reservado</c> com check
    /// <c>0 &lt;= saldo_reservado &lt;= saldo_atual</c>; os novos tipos de movimento (R, L, P) cabem
    /// na coluna <c>char(1)</c> existente. A concorrência otimista usa a coluna de sistema
    /// <c>xmin</c> do PostgreSQL, que já existe em toda tabela: o <c>AddColumn</c>/<c>DropColumn xmin</c>
    /// gerado pelo Npgsql foi removido à mão.
    /// </remarks>
    public partial class Fase4ReservaEstoque : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "saldo_reservado",
                schema: "estoque",
                table: "peca",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddCheckConstraint(
                name: "ck_peca_saldo_reservado_valido",
                schema: "estoque",
                table: "peca",
                sql: "saldo_reservado >= 0 AND saldo_reservado <= saldo_atual");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_peca_saldo_reservado_valido",
                schema: "estoque",
                table: "peca");

            migrationBuilder.DropColumn(
                name: "saldo_reservado",
                schema: "estoque",
                table: "peca");
        }
    }
}
