using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Oficina.Infraestrutura.Migrations
{
    /// <inheritdoc />
    public partial class InicialEstoque : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "estoque");

            migrationBuilder.CreateTable(
                name: "peca",
                schema: "estoque",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sku = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    nome = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    preco_unitario = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    saldo_atual = table.Column<int>(type: "integer", nullable: false),
                    ativo = table.Column<bool>(type: "boolean", nullable: false),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_peca", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "movimentacao",
                schema: "estoque",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<string>(type: "char(1)", nullable: false),
                    quantidade = table.Column<int>(type: "integer", nullable: false),
                    motivo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ordem_servico_id = table.Column<Guid>(type: "uuid", nullable: true),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    peca_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_movimentacao", x => x.id);
                    table.CheckConstraint("ck_movimentacao_quantidade_positiva", "quantidade > 0");
                    table.ForeignKey(
                        name: "FK_movimentacao_peca_peca_id",
                        column: x => x.peca_id,
                        principalSchema: "estoque",
                        principalTable: "peca",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_movimentacao_peca_data",
                schema: "estoque",
                table: "movimentacao",
                columns: new[] { "peca_id", "criado_em" });

            migrationBuilder.CreateIndex(
                name: "IX_peca_sku",
                schema: "estoque",
                table: "peca",
                column: "sku",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "movimentacao",
                schema: "estoque");

            migrationBuilder.DropTable(
                name: "peca",
                schema: "estoque");
        }
    }
}
