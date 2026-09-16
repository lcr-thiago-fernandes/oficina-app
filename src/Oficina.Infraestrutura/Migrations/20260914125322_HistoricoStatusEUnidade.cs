using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Oficina.Infraestrutura.Migrations
{
    /// <inheritdoc />
    public partial class HistoricoStatusEUnidade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ordem_servico_status",
                schema: "os",
                table: "ordem_servico");

            migrationBuilder.AddColumn<string>(
                name: "unidade",
                schema: "os",
                table: "ordem_servico",
                type: "character varying(60)",
                maxLength: 60,
                nullable: false,
                defaultValue: "matriz");

            migrationBuilder.CreateTable(
                name: "historico_status",
                schema: "os",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    status_anterior = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    status_novo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ocorrido_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    duracao_segundos = table.Column<long>(type: "bigint", nullable: true),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    ordem_servico_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_historico_status", x => x.id);
                    table.ForeignKey(
                        name: "FK_historico_status_ordem_servico_ordem_servico_id",
                        column: x => x.ordem_servico_id,
                        principalSchema: "os",
                        principalTable: "ordem_servico",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_ordem_servico_status_data",
                schema: "os",
                table: "ordem_servico",
                columns: new[] { "status", "criada_em" });

            migrationBuilder.CreateIndex(
                name: "ix_ordem_servico_unidade",
                schema: "os",
                table: "ordem_servico",
                column: "unidade");

            migrationBuilder.AddCheckConstraint(
                name: "ck_item_servico_preco_nao_negativo",
                schema: "os",
                table: "item_servico",
                sql: "preco_snapshot >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_item_servico_quantidade_positiva",
                schema: "os",
                table: "item_servico",
                sql: "quantidade > 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_item_peca_preco_nao_negativo",
                schema: "os",
                table: "item_peca",
                sql: "preco_snapshot >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_item_peca_quantidade_positiva",
                schema: "os",
                table: "item_peca",
                sql: "quantidade > 0");

            migrationBuilder.CreateIndex(
                name: "ix_historico_os",
                schema: "os",
                table: "historico_status",
                columns: new[] { "ordem_servico_id", "ocorrido_em" });

            migrationBuilder.CreateIndex(
                name: "ix_historico_status_data",
                schema: "os",
                table: "historico_status",
                columns: new[] { "status_anterior", "ocorrido_em" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "historico_status",
                schema: "os");

            migrationBuilder.DropIndex(
                name: "ix_ordem_servico_status_data",
                schema: "os",
                table: "ordem_servico");

            migrationBuilder.DropIndex(
                name: "ix_ordem_servico_unidade",
                schema: "os",
                table: "ordem_servico");

            migrationBuilder.DropCheckConstraint(
                name: "ck_item_servico_preco_nao_negativo",
                schema: "os",
                table: "item_servico");

            migrationBuilder.DropCheckConstraint(
                name: "ck_item_servico_quantidade_positiva",
                schema: "os",
                table: "item_servico");

            migrationBuilder.DropCheckConstraint(
                name: "ck_item_peca_preco_nao_negativo",
                schema: "os",
                table: "item_peca");

            migrationBuilder.DropCheckConstraint(
                name: "ck_item_peca_quantidade_positiva",
                schema: "os",
                table: "item_peca");

            migrationBuilder.DropColumn(
                name: "unidade",
                schema: "os",
                table: "ordem_servico");

            migrationBuilder.CreateIndex(
                name: "IX_ordem_servico_status",
                schema: "os",
                table: "ordem_servico",
                column: "status");
        }
    }
}
